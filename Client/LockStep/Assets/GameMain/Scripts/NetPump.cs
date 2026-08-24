using GameMain.Net;
using UnityEngine;

public class NetPump : MonoBehaviour
{
    KcpClientTransport client;
    LockstepSession session;

    void Start()
    {
        client = new KcpClientTransport();
        session = new LockstepSession(client);
        session.Log = OnSessionLog;
        client.Connect("127.0.0.1", 7777);
        Debug.Log("[LockStep] connecting 127.0.0.1:7777");
    }

    void Update()
    {
        if (client == null)
        {
            return;
        }

        client.Tick();
        if (session != null)
        {
            session.Tick();
        }
    }

    void OnSessionLog(string msg)
    {
        Debug.Log(msg);
    }

    void OnDisable()
    {
        StopClient();
    }

    void OnApplicationQuit()
    {
        StopClient();
    }

    void OnDestroy()
    {
        StopClient();
    }

    void StopClient()
    {
        session = null;
        if (client == null)
        {
            return;
        }

        client.Close();
        client = null;
    }
}
