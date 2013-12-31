using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MainControl : MonoBehaviour
{
    public GUISkin skin;
    public Texture2D manTex;

    float optimizedWidth = 480;
    float Scale { 
        get{
            return Screen.width / optimizedWidth;
        }
    }

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

    enum State {TitleScreen, WaitingRoom, Countdown, Playing, End };
    private State state = State.WaitingRoom;

    private NetworkPlayer blockingPlayer;
   
    void Awake()
    {
        SetState(State.TitleScreen);
        
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
        else if (state == State.Countdown)
        {
            countDown -= Time.deltaTime;
            if (countDown < 0)
            {
                SetState(State.Playing);
            }
        }
        //else if (state == State.WaitingRoom)
        //{
        //    HostData[] hosts = MasterServer.PollHostList();

        //    if (hosts.Length > 0 && playerName.Length > 0)
        //        Network.Connect(hosts[0]);
        //}
    }

    private void SetState(State newState)
    {
        state = newState;

        if(state == State.Countdown){
            countDown = 3;
        }
        else if (state == State.WaitingRoom)
        {
            MasterServer.ClearHostList();
            MasterServer.RequestHostList("Bomb");
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
    void PlayerReady(NetworkPlayer player, bool ready, string name)
    {
        playerStatuses[player].ready = ready;
        playerStatuses[player].name = name;

        Debug.Log("Player ready: " + playerStatuses[player].name + " " + ready);

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

    [RPC]
    void SetBlocking(NetworkPlayer player, NetworkPlayer blockedPlayer)
    {
        playerStatuses[player].blocking = blockedPlayer;
        Debug.Log("Player " + playerStatuses[player].name + " is blocking " + playerStatuses[blockedPlayer].name);
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
        else if (msEvent == MasterServerEvent.HostListReceived)
        {
            HostData[] hosts = MasterServer.PollHostList();

            if (hosts.Length > 0)
            {
                Network.Connect(hosts[0]);
            }
            else
            {
                Network.InitializeServer(32, 25002, false);
            }
        }
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
        GUI.skin = skin;
        if (state == State.TitleScreen)
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", GUI.skin.GetStyle("TitleBackground"));

            GUI.DrawTexture(new Rect(0, 0, Screen.width, 2 * Screen.height / 3), GUI.skin.GetStyle("TitleLogo").normal.background, ScaleMode.ScaleToFit);
            GUIStyle buttonStyle = GUI.skin.GetStyle("PlayButton");
            buttonStyle.fontSize = (int)(46 * Scale);

            if (GUI.Button(new Rect(0, 2* Screen.height / 3, Screen.width, Screen.height / 3), "play", buttonStyle))
            {
                SetState(State.WaitingRoom);
            }
        }
        else if (state == State.WaitingRoom)
        {
            GUIStyle textStyle = GUI.skin.GetStyle("PlayButton");
            textStyle.fontSize = (int)(46 * Scale);

            GUI.Label(new Rect(0, 0, Screen.width, Screen.height / 4), "Enter your name", textStyle);

            GUIStyle textFieldStyle = GUI.skin.GetStyle("EnterName");
            textFieldStyle.fontSize = (int)(46 * Scale);
            playerName = GUI.TextField(new Rect(Screen.width * 0.1f, Screen.height / 4, Screen.width - Screen.width * 0.2f, Screen.height / 5), playerName, 8, textFieldStyle);

            NetworkPlayer[] players = playerStatuses.Keys.ToArray();

            if (players.Length > 0)
            {
                float maxWidth = (Screen.height / 4) * 232 / 600f;
                float manWidth = Mathf.Clamp(Screen.width / players.Length, 0, maxWidth);
                float manOffsetX = (Screen.width - players.Length * manWidth) / 2;

                for (int i = 0; i < players.Length; i++)
                    GUI.DrawTexture(new Rect(i * manWidth + manOffsetX, 2 * Screen.height / 4, manWidth, Screen.height / 5), manTex, ScaleMode.ScaleToFit);
            }

            if (isConnected)
            {
                GUIStyle readyButtonStyle = GUI.skin.GetStyle("ReadyButton");
                readyButtonStyle.fontSize = (int)(10 * Screen.height/200f);
                if (!isReady)
                    readyButtonStyle.normal.background = readyButtonStyle.onNormal.background;
                else
                    readyButtonStyle.normal.background = readyButtonStyle.onActive.background;


                if (GUI.Button(new Rect(Screen.width * 0.1f, 3 * Screen.height / 4, Screen.width - Screen.width * 0.2f, Screen.height / 4 - Screen.width * 0.1f), "ready please", readyButtonStyle))
                {
                    isReady = !isReady;
                    networkView.RPC("PlayerReady", RPCMode.AllBuffered, Network.player, isReady, playerName);
                }
            }
            else
            {
                textStyle.fontSize = (int)(24 * Scale);
                GUI.Label(new Rect(0, 3 * Screen.height / 4, Screen.width, Screen.height / 4), "Connecting...", textStyle);
            }
        }
        else if (state == State.Playing)
        {
            //GUILayout.Label("Bomb time: " + bombTime);

            if (playerStatuses[Network.player].hasBomb)
            {
                GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", GUI.skin.GetStyle("BombBackground"));
            }
            else
            {
                GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", GUI.skin.GetStyle("NoBombBackground"));
            }

            NetworkPlayer[] players = playerStatuses.Keys.ToArray();

            int nButtonsX = 3;
            int nButtonsY = 2;

            if (players.Length == 2)
                nButtonsX = 1;
            else if (players.Length == 3 || players.Length == 4)
                nButtonsX = 2;
            else if (players.Length == 5 || players.Length == 6)
                nButtonsX = 3;
            else if (players.Length == 7 || players.Length == 8)
                nButtonsX = 4;

            float buttonScale = Screen.height / 4 < Screen.width / 3 ? Screen.height / 4 : Screen.width / 3;

            float buttonOffsetX = (Screen.width - buttonScale * nButtonsX) / 2;
            float buttonOffsetY = (Screen.height / 2 - buttonScale * nButtonsY) / 2;

            GUIStyle buttonStyle = GUI.skin.GetStyle("NameButton");
            buttonStyle.fontSize = (int)(23 * buttonScale / 200f);



            for (int i = 0; i < players.Length; i++)
            {
                float x = (i % nButtonsX) * buttonScale + buttonOffsetX;
                float y = (i / nButtonsY) * buttonScale + buttonOffsetY + Screen.height / 2;

                NetworkPlayer otherPlayer = players[i];
                if (otherPlayer != Network.player)
                {
                    if (playerStatuses[Network.player].hasBomb)
                    {
                        if (GUI.Button(new Rect(x, y, buttonScale, buttonScale), playerStatuses[otherPlayer].name, buttonStyle))
                        {
                            if (playerStatuses[otherPlayer].blocking != Network.player)
                            {
                                playerStatuses[Network.player].hasBomb = false;
                                networkView.RPC("SendBomb", RPCMode.All, otherPlayer);
                            }
                        }
                    }
                    else
                    {
                        if (GUI.RepeatButton(new Rect(x, y, buttonScale, buttonScale), playerStatuses[otherPlayer].name, buttonStyle))
                        {
                            if (blockingPlayer == Network.player)
                            {
                                blockingPlayer = otherPlayer;
                                networkView.RPC("SetBlocking", RPCMode.All, Network.player, otherPlayer);
                            }
                        }
                    }
                }
            }

            if (blockingPlayer != Network.player && !Input.GetMouseButton(0))
            {
                blockingPlayer = Network.player;
                networkView.RPC("SetBlocking", RPCMode.All, Network.player, Network.player);
            }

            GUIStyle textStyle = GUI.skin.GetStyle("BombText");
            textStyle.fontSize = (int)(23 * (Screen.height / 4) / 100f);
            textStyle.padding.left = Screen.width / 10;
            textStyle.padding.right = Screen.width / 10;

            GUIStyle bombIconStyle = GUI.skin.GetStyle("BombIcon");
            bombIconStyle.fixedWidth = bombIconStyle.fixedHeight = Screen.height / 4.5f;
            GUIStyle shieldIconStyle = GUI.skin.GetStyle("ShieldIcon");
            shieldIconStyle.fixedWidth = shieldIconStyle.fixedHeight = Screen.height / 4.5f;

            float iconOffsetX = (Screen.width - Screen.height / 4) / 2;

            if (playerStatuses[Network.player].hasBomb)
            {
                GUI.Label(new Rect(0, 0, Screen.width, Screen.height / 4), "YOU HAVE THE BOMB", textStyle);
                GUI.Box(new Rect(iconOffsetX, Screen.height / 4, Screen.width, Screen.height / 4), "", bombIconStyle);
            }
            else
            {
                GUI.Label(new Rect(0, 0, Screen.width, Screen.height / 4), "YOU DO NOT HAVE THE BOMB", textStyle);
                GUI.Box(new Rect(iconOffsetX, Screen.height / 4, Screen.width, Screen.height / 4), "", shieldIconStyle);
            }
        }
        else if (state == State.Countdown)
        {
            GUIStyle guiStyle = GUI.skin.GetStyle("CountdownBackground");
            guiStyle.fontSize = (int)(46 * Scale);

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "Don't be left with the bomb\n\n" + Mathf.CeilToInt(countDown) + "...", guiStyle);
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
        

        //if (!Input.GetMouseButton(0))
        //{
        //    if (isReady)
        //    {
        //        isReady = false;
        //        networkView.RPC("PlayerReady", RPCMode.All, Network.player, false);
        //    }
        //}
    }

    void OnFailedToConnectToMasterServer(NetworkConnectionError info)
    {
        Debug.Log("Could not connect to master server: " + info);
    }
}
