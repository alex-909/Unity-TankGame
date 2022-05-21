using System.Collections.Generic;
using UnityEngine;
using Mirror;
using DapperDino.Mirror.Tutorials.Lobby;
using DapperDino.Tutorials.Lobby;
using TMPro;
using System;
using System.Linq;

public class DeathManager : NetworkBehaviour
{
	#region System References

	private PlayerSpawnSystem spawnSystem;
	public PlayerSpawnSystem SpawnSystem
	{
		get
		{
			if (spawnSystem != null) { return spawnSystem; }
			return spawnSystem = (PlayerSpawnSystem)FindObjectOfType(typeof(PlayerSpawnSystem));
		}
	}

	private CreateMap mapCreator;
	private CreateMap MapCreator
	{
		get
		{
			if (mapCreator != null) { return mapCreator; }
			return mapCreator = FindObjectOfType<CreateMap>();
		}
	}

	private NetworkManagerTG room;
	private NetworkManagerTG Room
	{
		get
		{
			if (room != null) { return room; }
			return room = NetworkManager.singleton as NetworkManagerTG;
		}
	}

	#endregion

	public List<PlayerScript> AllPlayers { get; } = new List<PlayerScript>();
	public List<PlayerScript> AlivePlayers { get; } = new List<PlayerScript>();
	public List<PlayerScript> DeadPlayers { get; } = new List<PlayerScript>();

	[SerializeField] private Animator animator = null;
	[SerializeField] private GameObject WinnerUI;
	private string triggerName = "Active";

	[SerializeField] Color blueColor;
	[SerializeField] Color redColor;
	[SerializeField] Color greenColor;
	[SerializeField] Color yellowColor;

	[Server]
	public void Register(PlayerScript player)
	{
		//Debug.Log("added player to list");
		AlivePlayers.Add(player);
		AllPlayers.Add(player);

		SendScoreUpdate();
	}

	#region Score
	[Server]
	public void SendScoreUpdate()
	{
		List<ScoreManager.ScoreInfo> scoreInfos = new List<ScoreManager.ScoreInfo>();

		foreach (PlayerScript player in AllPlayers)
		{
			var playerScore = player.GetComponent<PlayerScore>();
			ScoreManager.ScoreInfo info = new ScoreManager.ScoreInfo
			{
				name = player.playerName,
				roundsWon = playerScore.GetStat(PlayerScore.Stat.RoundsWon),
				kills = playerScore.GetStat(PlayerScore.Stat.Kills),
				deaths = playerScore.GetStat(PlayerScore.Stat.Deaths),
				color = GetColor(player.GetComponent<ApplyMaterials>().tankColor)
			};

			scoreInfos.Add(info);
		}
		scoreInfos = scoreInfos.OrderByDescending(x => x.roundsWon).ToList<ScoreManager.ScoreInfo>();

		RpcSendScoreUpdate(scoreInfos.ToArray());
	}

	[ClientRpc] 
	private void RpcSendScoreUpdate(ScoreManager.ScoreInfo[] scoreInfos)
	{
		ScoreManager scoreManager = FindObjectOfType<ScoreManager>();
		scoreManager.UpdateScores(scoreInfos);
	}
	#endregion

	#region PlayerDeath
	public void Died(PlayerScript player)
	{
		SendScoreUpdate();
		//Debug.Log("Player died!");

		for (int i = 0; i < AlivePlayers.Count; i++)
		{
			if (AlivePlayers[i].Equals(player))
			{
				//Debug.Log("found the player");
				DeadPlayers.Add(player);
				AlivePlayers.Remove(player);

				RpcDisablePlayer(player);
				CheckForRestart();
			}
		}
	}
	[ClientRpc]
	private void RpcDisablePlayer(PlayerScript player)
	{
		player.gameObject.SetActive(false);
	}

	[Server]
	private void CheckForRestart()
	{
		if (AlivePlayers.Count > 1) { return; }

		AlivePlayers[0].ScoreManager.IncreaseStat(PlayerScore.Stat.RoundsWon);
		SendScoreUpdate();
		RpcShowWinner(AlivePlayers[0]);
	}
	#endregion


	#region Winner + Animation

	[ClientRpc]
	private void RpcShowWinner(PlayerScript player)
	{
		CmdDestroyAllBullets();

		var textColor = WinnerUI.GetComponent<TextMeshProUGUI>();
		textColor.color = GetColor(player.GetComponent<ApplyMaterials>().tankColor);
		textColor.text = $"{player.playerName} has won the Round!";

		animator.enabled = true;
		animator.SetTrigger(triggerName);
	}
	public void AnimationEnded()
	{
		Debug.Log("animation end call");
		animator.enabled = false;
		WinnerUI.SetActive(false);

		CmdReloadScene();
	}

	[Command(requiresAuthority = false)]
	private void CmdReloadScene() 
	{
		Room.ServerChangeScene("Scene_Map_01");
	}

	private Color GetColor(TankColor tankColor)
	{
		return tankColor switch
		{
			TankColor.Blue => blueColor,
			TankColor.Red => redColor,
			TankColor.Green => greenColor,
			TankColor.Yellow => yellowColor,
			_ => Color.white,
		};
	}

	#endregion

	#region DestroyBullets
	[Command]
	private void CmdDestroyAllBullets() 
	{
		RpcDestroyAllBullets();
	}
	[ClientRpc]
	private void RpcDestroyAllBullets()
	{
		//RpcDestroyAllBullets();
		Debug.Log("destroying all the bullets");
		BulletMove[] bullets = FindObjectsOfType<BulletMove>();
		foreach (BulletMove bullet in bullets)
		{
			if (bullet) { NetworkServer.Destroy(bullet.gameObject); }
		}
	}
	#endregion

	#region Unused right now:

	private static System.Random rng = new System.Random();
	private Transform[] ShuffleSpawnPoints(List<Transform> points)
	{
		var shuffledPoints = points.OrderBy(a => rng.Next());
		return shuffledPoints.ToArray<Transform>();
	}

	[ClientRpc]
	private void RpcActivatePlayer(PlayerScript player)
	{
		player.gameObject.SetActive(true);
	}

	[Command(requiresAuthority = false)]
	private void CmdRespawn()
	{
		//RespawnAllPlayers();
	}


	[Server]
	private void ResetMap()
	{
		MapCreator.ResetMap();
	}

	[Server]
	private void RespawnAllPlayers()
	{
		AlivePlayers.Clear();
		AlivePlayers.AddRange(AllPlayers);

		List<Transform> spawnPointsList = PlayerSpawnSystem.spawnPoints;

		//Transform[] spawnPoints = ShuffleSpawnPoints(spawnPointsList);

		//CmdDestroyAllBullets();

		//ResetMap();

		Debug.Log("respawning all players");

		for (int i = 0; i < AllPlayers.Count; i++)
		{
			//Debug.Log("in loop: i = " + i);
			var player = AllPlayers[i];

			//player.BlockInput();
			//player.SetPosition(spawnPoints[i].position, spawnPoints[i].rotation);
			//RpcActivatePlayer(player);
			//player.Respawn();
		}

		AlivePlayers.Clear();
		AlivePlayers.AddRange(AllPlayers);
		DeadPlayers.Clear();

		Debug.Log("enabling round animator");
		//RoundSystem.EnableAnimator();
	}
	#endregion

}
