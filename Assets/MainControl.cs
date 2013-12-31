using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MainControl : MonoBehaviour
{
    public GUISkin skin;
    public Texture2D manTex, winTex, loseTex;

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

    private float bombTime = 0, prevBombTime = 0;
    private float bombTimeSlice = 1;
    private float bombMeanLifetime = 30;

    private bool isSending;
    private bool isReceiving;
    NetworkPlayer sendingTo;
    private float sendTime = 0.25f;
    private float sendTimer = 0;
    private float receiveTime = 2f;
    private float receiveTimer = 0;
    private bool isReturning;
    private float returnTime = 0.5f;
    private float returnTimer = 0;

    private float bombIconPos;

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
            if (Network.isServer)
            {

                bombTime += Time.deltaTime;
                bombTime %= bombTimeSlice;

                if (prevBombTime - bombTime > 0.8f)
                {
                    if (Random.value < 0/*1 / bombMeanLifetime*/)
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

                prevBombTime = bombTime;
            }

            if (isSending)
            {
                sendTimer += Time.deltaTime;
                if (sendTimer > sendTime)
                {
                    if (playerStatuses[sendingTo].blocking != Network.player)
                    {
                        playerStatuses[Network.player].hasBomb = false;
                        networkView.RPC("SetBombHolder", RPCMode.All, sendingTo);
                    }
                    else
                    {
                        networkView.RPC("SendBombBlocked", sendingTo, Network.player, returnTime);
                        isReturning = true;
                        returnTimer = 0;
                    }
                    isSending = false;
                    sendTimer = 0;
                }

                bombIconPos = (Screen.width / 2f) * (1 - sendTimer / (sendTime / 2));
            }

            if (isReceiving)
            {

                if (receiveTimer > receiveTime)
                {
                    isReceiving = false;
                    bombIconPos = 0;
                    returnTimer = 0;
                }
                else
                {
                    receiveTimer += Time.deltaTime;
                    bombIconPos = (Screen.width / 2f) * (1 - receiveTimer / (receiveTime / 2)) + Screen.width;
                }
            }

            if (isReturning)
            {


                if (returnTimer > returnTime)
                {
                    isReturning = false;
                }
                else
                {
                    returnTimer += Time.deltaTime;
                    bombIconPos = (Screen.width / 2f) * (returnTimer / (returnTime / 2));
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

        networkView.RPC("BeginGame", RPCMode.All, startingPlayer);
    }

    [RPC]
    void BeginGame(NetworkPlayer startingPlayer)
    {
        isReady = false;
        playerStatuses[startingPlayer].hasBomb = true;
        SetState(State.Countdown);
        bombTime = 0;
        prevBombTime = 0;
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
    void SendBomb(float time)
    {
        isReceiving = true;
        receiveTime = time;
        receiveTimer = 0;
        bombIconPos = (Screen.width / 2f) + Screen.width;
    }

    [RPC]
    void SendBombBlocked(NetworkPlayer sender, float time)
    {
        isReceiving = false;
        isReturning = true;
        returnTime = time;
        returnTimer = 0;
        bombIconPos = 0;
        Debug.Log("Bomb blocked");
    }

    [RPC]
    void SetBombHolder(NetworkPlayer player)
    {
        playerStatuses[player].hasBomb = true;
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
            int nButtonsY = 1;

            if (players.Length == 2)
            {
                nButtonsX = 1;
                nButtonsY = 1;
            }
            else if (players.Length == 3)
            {
                nButtonsX = 2;
                nButtonsY = 1;
            }
            else if (players.Length == 4 || players.Length == 5)
            {
                nButtonsX = 2;
                nButtonsY = 2;
            }
            else if (players.Length == 6 || players.Length == 7)
            {
                nButtonsX = 3;
                nButtonsY = 2;
            }
            else if (players.Length == 8)
            {
                nButtonsX = 4;
                nButtonsY = 2;
            }

            float buttonScale = Screen.height / 4 < Screen.width / 3 ? Screen.height / 4 : Screen.width / 3;

            float buttonOffsetX = (Screen.width - buttonScale * nButtonsX) / 2;
            float buttonOffsetY = (Screen.height / 2 - buttonScale * nButtonsY) / 2;

            GUIStyle buttonStyle = GUI.skin.GetStyle("NameButton");
            buttonStyle.fontSize = (int)(23 * buttonScale / 200f);

            int i = 0;
            for (int p = 0; p < players.Length; p++)
            {    
                NetworkPlayer otherPlayer = players[p];
                if (otherPlayer != Network.player)
                {
                    float x = (i % nButtonsX) * buttonScale + buttonOffsetX;
                    float y = (i / nButtonsX) * buttonScale + buttonOffsetY + Screen.height / 2;

                    if (playerStatuses[Network.player].hasBomb)
                    {
                        if (GUI.Button(new Rect(x, y, buttonScale, buttonScale), playerStatuses[otherPlayer].name, buttonStyle))
                        {
                            if (!isSending && !isReturning)
                            {
                                isSending = true;
                                sendingTo = otherPlayer;
                                networkView.RPC("SendBomb", otherPlayer, sendTime);
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

                    i++;
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

            if (playerStatuses[Network.player].hasBomb)
            {
                float bombIconOffsetX = (Screen.width - Screen.height / 4) / 2;
                if (isSending)
                    bombIconOffsetX = bombIconPos - bombIconStyle.fixedWidth / 2;
                else if (isReturning)
                    bombIconOffsetX = bombIconPos - bombIconStyle.fixedWidth / 2 - Screen.width/2;

                GUI.Label(new Rect(0, 0, Screen.width, Screen.height / 4), "YOU HAVE THE BOMB", textStyle);
                GUI.Box(new Rect(bombIconOffsetX, Screen.height / 4, Screen.width, Screen.height / 4), "", bombIconStyle);
            }
            else
            {
                float shieldIconOffsetX = (Screen.width - Screen.height / 4) / 2;

                GUI.Label(new Rect(0, 0, Screen.width, Screen.height / 4), "YOU DO NOT HAVE THE BOMB", textStyle);
                GUI.Box(new Rect(shieldIconOffsetX, Screen.height / 4, Screen.width, Screen.height / 4), "", shieldIconStyle);

                if (isReceiving)
                {
                    float bombIconOffsetX = bombIconPos - bombIconStyle.fixedWidth / 2;
                    GUI.Box(new Rect(bombIconOffsetX, Screen.height / 4, Screen.width, Screen.height / 4), "", bombIconStyle);
                }

                if (isReturning)
                {
                   float bombIconOffsetX = bombIconPos - bombIconStyle.fixedWidth / 2 + Screen.width/2;
                   GUI.Box(new Rect(bombIconOffsetX, Screen.height / 4, Screen.width, Screen.height / 4), "", bombIconStyle);
                }
            }
        }
        else if (state == State.Countdown)
        {
            GUIStyle guiStyle = GUI.skin.GetStyle("CountdownBackground");
            guiStyle.fontSize = (int)(46 * Scale);

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "Do not be left with the bomb\n\n" + Mathf.CeilToInt(countDown) + "...", guiStyle);
        }
        else if (state == State.End)
        {
            if (isLoser)
            {
                GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", GUI.skin.GetStyle("BombBackground"));
                GUI.DrawTexture(new Rect(0, 0, Screen.width, 2 * Screen.height / 3), loseTex, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", GUI.skin.GetStyle("NoBombBackground"));
                GUI.DrawTexture(new Rect(0, 0, Screen.width, 2 * Screen.height / 3), winTex, ScaleMode.ScaleToFit);
            }

            GUIStyle readyButtonStyle = GUI.skin.GetStyle("ReadyButton");
            readyButtonStyle.fontSize = (int)(10 * Screen.height / 200f);
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
