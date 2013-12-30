using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MainControl : MonoBehaviour
{
    private bool isConnected = false;
    private bool isReady;
    private int playerId;
    private bool isRegistered;
    IDictionary<NetworkPlayer, PlayerStatus> playerStatuses;
    private float minBombTime = 15, maxBombTime = 30;
    private float bombTime;

    string playerName = "player";
    private bool isLoser;

    float countDown;

    enum State { WaitingRoom, Countdown, Playing, End };
    private State state = State.WaitingRoom;
   
    void Awake()
    {
        SetState(State.WaitingRoom);
        MasterServer.ClearHostList();
        MasterServer.RequestHostList("Bomb");
        playerStatuses = new Dictionary<NetworkPlayer, PlayerStatus>();
    }

    void Start()
    {
     

    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) 
        { 
            Application.Quit(); 
        }

        if (state == State.Playing)
        {
            bombTime -= Time.deltaTime;

            if (Network.isServer)
            {
                if (bombTime < 0)
                {
      
                    foreach (NetworkPlayer player in playerStatuses.Keys)
                    {
                        if (playerStatuses[player].hasBomb)
                        {
                            networkView.RPC("EndGame", RPCMode.All, player);
                            break;
                        }
                    }
                }
            }
        }
    }

    private void SetState(State newState)
    {
        state = newState;

        if(state == State.Countdown){
            countDown = 3;
        }
    }

    private void InitGame()
    {
        List<NetworkPlayer> players = Enumerable.ToList(playerStatuses.Keys);
        NetworkPlayer startingPlayer = players[Random.Range(0, playerStatuses.Count)];

        float initBombTime = Random.Range(minBombTime, maxBombTime);

        networkView.RPC("BeginGame", RPCMode.All, startingPlayer, initBombTime);
    }

    [RPC]
    void BeginGame(NetworkPlayer startingPlayer, float initBombTime)
    {
        isReady = false;
        bombTime = initBombTime;
        playerStatuses[startingPlayer].hasBomb = true;
        SetState(State.Countdown);
    }

   

    [RPC]
    void EndGame(NetworkPlayer loser)
    {
        if (loser == Network.player)
        {
            isLoser = true;
        }

        SetState(State.End);
    }

    [RPC]
    void InitPlayer(int id)
    {
        playerId = id;
        isConnected = true;
        networkView.RPC("PlayerAdded", RPCMode.AllBuffered, Network.player, playerName);
    }

    [RPC]
    void PlayerAdded(NetworkPlayer player, string name)
    {
        playerStatuses[player] = new PlayerStatus();
        playerStatuses[player].name = name;
    }

    [RPC]
    void PlayerRemoved(NetworkPlayer player)
    {
        playerStatuses.Remove(player);
    }

    [RPC]
    void PlayerReady(NetworkPlayer player, bool ready)
    {
        Debug.Log("Player ready: " + playerStatuses[player].name + " " + ready);
        playerStatuses[player].ready = ready;

        if (Network.isServer && playerStatuses.Count > 1)
        {
            bool allReady = true;
            foreach (PlayerStatus playerStatus in playerStatuses.Values)
            {
                if (!playerStatus.ready)
                {
                    allReady = false;
                    break;
                }
            }

            if (allReady)
            {
                InitGame();
            }
        }

        
    }

    [RPC]
    void SendBomb(NetworkPlayer toPlayer)
    {
        playerStatuses[toPlayer].hasBomb = true;
    }

    void OnServerInitialized()
    {
        MasterServer.RegisterHost("Bomb", "Bomb game", "Default bomb game");
    }

    void OnConnectedToServer()
    {
        Debug.Log("Connected to server");
    }

    void OnFailedToConnect(NetworkConnectionError error) {
		Debug.Log("Could not connect to server: "+ error);
	}

    void OnMasterServerEvent(MasterServerEvent msEvent)
    {
        
        if (msEvent == MasterServerEvent.RegistrationSucceeded && !isRegistered)
        {
            Debug.Log("Server registered");
            isRegistered = true;
            OnPlayerConnected(Network.player);
        }

        //else if (msEvent == MasterServerEvent.HostListReceived)
        //{
           
        //    HostData[] hosts = MasterServer.PollHostList();
 
        //    if (hosts.Length == 0)
        //    {
        //        Network.InitializeServer(32, 25002, false);
        //    }
        //    else
        //    {
        //        Network.Connect(hosts[0]);
        //    }
        //}
    }

    
    void OnPlayerConnected(NetworkPlayer player)
    {
        Debug.Log("Player " + playerStatuses.Keys.Count + " connected from " + player.ipAddress + ":" + player.port);

        if (Network.player == player)
            InitPlayer(playerStatuses.Keys.Count);
        else
            networkView.RPC("InitPlayer", player, playerStatuses.Keys.Count);
        
    }

    void OnPlayerDisconnected(NetworkPlayer player) {
        networkView.RPC("PlayerRemoved", RPCMode.All, player);

		Debug.Log("Clean up after player " +  player);
		Network.RemoveRPCs(player);
		Network.DestroyPlayerObjects(player);
        Debug.Log("Player " + playerStatuses.Keys.Count + " disconnected");


	}

    void OnGUI()
    {
        if (state == State.Playing)
        {
            GUILayout.Label("Bomb time: " + bombTime);

            foreach (NetworkPlayer otherPlayer in playerStatuses.Keys)
            {
                if (otherPlayer != Network.player)
                {
                    if (GUILayout.Button(playerStatuses[otherPlayer].name))
                    {
                        if (playerStatuses[Network.player].hasBomb)
                        {
                            playerStatuses[Network.player].hasBomb = false;
                            networkView.RPC("SendBomb", RPCMode.All, otherPlayer);
                        }
                    }
                }
            }

            if (playerStatuses[Network.player].hasBomb)
                GUILayout.Label("YOU HAVE THE BOMB!");
        }
        else if (state == State.Countdown)
        {
            GUILayout.Label("" + Mathf.CeilToInt(countDown));

            countDown -= Time.deltaTime;
            if (countDown < 0)
            {
                SetState(State.Playing);
            }
        }
        else if (state == State.End)
        {
            if (isLoser)
            {
                GUILayout.Label("You Lose!");
            }
            else
            {
                GUILayout.Label("You Win!");
            }

            if (GUILayout.RepeatButton("Hold to replay"))
            {
                if (!isReady && Input.GetMouseButton(0))
                {
                    isReady = true;
                    networkView.RPC("PlayerReady", RPCMode.All, Network.player, true);
                }
            }
        }
        else if (state == State.WaitingRoom)
        {
            if (!isConnected)
            {
                playerName = GUILayout.TextField(playerName, 8);

                HostData[] hosts = MasterServer.PollHostList();
                foreach (var host in hosts)
                {
                    GUILayout.BeginHorizontal();
                    var name = host.gameName + " " + host.connectedPlayers + " / " + host.playerLimit;
                    GUILayout.Label(name);
                    GUILayout.Space(5);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Connect"))
                    {
                        if (playerName.Length > 0)
                            Network.Connect(host);
                    }
                    GUILayout.EndHorizontal();
                }

                if (GUILayout.Button("Host Game"))
                {
                    if (playerName.Length > 0)
                        Network.InitializeServer(32, 25002, false);
                }
            }
            else
            {
                GUILayout.Label("Players connected: " + playerStatuses.Keys.Count);
                GUILayout.Label("Player id: " + playerId);
                if (GUILayout.RepeatButton("Ready"))
                {
                    if (!isReady && Input.GetMouseButton(0))
                    {
                        isReady = true;
                        networkView.RPC("PlayerReady", RPCMode.All, Network.player, true);
                    }
                }
            }
        }

        if (!Input.GetMouseButton(0))
        {
            if (isReady)
            {
                isReady = false;
                networkView.RPC("PlayerReady", RPCMode.All, Network.player, false);
            }
        }
    }

    void OnFailedToConnectToMasterServer(NetworkConnectionError info)
    {
        Debug.Log("Could not connect to master server: " + info);
    }
}
