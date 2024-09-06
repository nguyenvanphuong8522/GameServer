using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using UnityEngine;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Random = UnityEngine.Random;
using Newtonsoft.Json;
using UnityEngine.Windows;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;




public class Server : MonoBehaviour
{
    #region Init
    [SerializeField] private Dictionary<int, Socket> dictionaryClientSocket = new Dictionary<int, Socket>();

    private SpawnManager spawnManager;

    private Socket listenSocket;

    private List<Player> listOfPlayer = new List<Player>();

    private void Awake()
    {
        spawnManager = GetComponent<SpawnManager>();
    }

    private void Start()
    {
        CallApi();
        InitSocket();
        var t = Task.Run(() => WaitConnect());
    }
    private void InitSocket()
    {
        IPEndPoint ipEndPoint = new(IPAddress.Loopback, 8522);

        listenSocket = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        listenSocket.Bind(ipEndPoint);
        listenSocket.Listen(100);
        Debug.Log("LISTENING...");
    }

    #endregion

    #region Call API
    private static HttpClient _httpClient;

    public static void CallApi()
    {
        _httpClient = new HttpClient();
        Task callTask = CallRestApiAsync();
    }

    public static async Task CallRestApiAsync()
    {
        string url = "https://localhost:7087/api/category";

        HttpResponseMessage response = await _httpClient.GetAsync(url);

        if (response.IsSuccessStatusCode)
        {
            string responseBody = await response.Content.ReadAsStringAsync();
            //Debug.LogError(responseBody);
        }
        else
        {
            Debug.LogError($"Request failed with status code: {response.StatusCode}");
        }
    }
    #endregion


    private async Task WaitConnect()
    {
        while (true)
        {
            var socket = await listenSocket.AcceptAsync();
            System.Random random = new System.Random();

            int randomKey = random.Next(1, 1001);
            dictionaryClientSocket.Add(randomKey, socket);
            await SendToSingleClient(socket, randomKey.ToString());
            var taskListen = ListenClient(socket);
        }
    }

    private async Task SpawnNewPlayer(int index, Socket socket)
    {
        await UniTask.SwitchToMainThread();


        Player newPlayer = spawnManager.GetPrefab(index);
        listOfPlayer.Add(newPlayer);

        string inforNewPlayer = ConvertToJson(newPlayer, RequestType.CREATE);
        await SendToSingleClient(socket, inforNewPlayer);
        await SendToAllClient(inforNewPlayer);
        if (listOfPlayer.Count > 1)
        {
            await SendInforOldPlayers(socket);
        }


        await UniTask.SwitchToThreadPool();
    }

    private async Task SendToSingleClient(Socket socket, string message)
    {
        string messageSpecify = message + '@';
        var sendBuffer = Encoding.UTF8.GetBytes(messageSpecify);
        await socket.SendAsync(sendBuffer, SocketFlags.None);
    }

    private async Task SendToAllClient(string message)
    {
        foreach (var item in dictionaryClientSocket)
        {
            await SendToSingleClient(item.Value, message);
        }
    }

    private async Task SendInforOldPlayers(Socket socket)
    {
        foreach (Player item in listOfPlayer)
        {
            await SendToSingleClient(socket, ConvertToJson(item, RequestType.CREATE));
        }
    }

    async Task ListenClient(Socket clientSocket)
    {
        while (true)
        {
            byte[] buffer = new byte[1024];
            int messageCode = await clientSocket.ReceiveAsync(buffer, SocketFlags.None);
            string messageReceived = Encoding.UTF8.GetString(buffer, 0, messageCode);
            if (messageCode == 0) return;
            string[] requests = ConvertToArrayString(messageReceived);
            var t = HandleManyRequest(requests, clientSocket);
        }
    }

    private async Task HandleOneRequest(string request, Socket clientSocket)
    {
        //Debug.Log(request);
        if (!string.IsNullOrEmpty(request))
        {
            MyVector3 dataNewPlayer = JsonConvert.DeserializeObject<MyVector3>(request);
            if (dataNewPlayer != null)
            {
                if (dataNewPlayer.type == RequestType.CREATE)
                {
                    var key = dictionaryClientSocket.FirstOrDefault(kvp => kvp.Value == clientSocket).Key;
                    await SpawnNewPlayer(0, dictionaryClientSocket[key]);
                }
                else if (dataNewPlayer.type == RequestType.POSITION)
                {
                    Player player = listOfPlayer.Find(x => x.Id == dataNewPlayer.id);
                    Player sendPlayer = player;
                    if (player != null)
                    {
                        await UniTask.SwitchToMainThread();
                        player.transform.position = new Vector3(dataNewPlayer.x, dataNewPlayer.y, dataNewPlayer.z);

                        await SendToAllClient(ConvertToJson(player, dataNewPlayer.type));
                        await UniTask.SwitchToThreadPool();
                    }
                }
                else
                {
                    var key = dictionaryClientSocket.FirstOrDefault(x => x.Value == clientSocket).Key;
                    await RemovePlayer(dataNewPlayer.id);
                    dictionaryClientSocket.Remove(key);
                }

            }
        }
    }

    private async Task HandleManyRequest(string[] requests, Socket clientSocket)
    {
        foreach (string request in requests)
        {
            await HandleOneRequest(request, clientSocket);
        }
    }

    private string[] ConvertToArrayString(string message)
    {
        string[] array = message.Split('@').Where(part => !string.IsNullOrWhiteSpace(part)).ToArray(); ;
        return array;
    }

    private async Task RemovePlayer(int id)
    {
        Player player = listOfPlayer.Find(x => x.Id == id);
        listOfPlayer.Remove(player);
        await UniTask.SwitchToMainThread();
        Destroy(player.gameObject);
        await UniTask.SwitchToThreadPool();
    }


    private string ConvertToJson(Player player, RequestType type)
    {
        Vector3 curPos = player.transform.position;
        MyVector3 newVector3 = new MyVector3(curPos.x, curPos.y, curPos.z, type);
        newVector3.id = player.Id;
        newVector3.type = type;
        string result = JsonConvert.SerializeObject(newVector3);
        return result;
    }


    private void OnDestroy()
    {
        foreach (var item in dictionaryClientSocket)
        {
            item.Value.Shutdown(SocketShutdown.Both);
        }
    }
}