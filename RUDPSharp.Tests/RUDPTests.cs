using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RUDPSharp;

namespace RUDPSharp.Tests
{
    public class RUDPTests {

        MockUDPSocket clientSocket;
        MockUDPSocket serverSocket;

        IPEndPoint serverAny = new IPEndPoint(IPAddress.Loopback, 8000);
        IPEndPoint clientAny = new IPEndPoint(IPAddress.Loopback, 8001);

        protected static void WaitFor(int milliseconds)
		{
			var pause = new ManualResetEvent(false);
			pause.WaitOne(milliseconds);
		}
        
        [SetUp]
        public void Setup ()
        {
            clientSocket = new MockUDPSocket ("MockClient");
            serverSocket = new MockUDPSocket ("MockServer");
            serverSocket.Link (clientSocket);
        }

        // [Test]
        // public async Task ClientExample ()
        // {
        //     using (var rUDPClient = new RUDP<MockUDPSocket>(clientSocket)) {
        //         rUDPClient.Start (8001);
        //         bool result = await rUDPClient.ConnectAsync (serverAny.Address.ToString (), 8000);
        //         if (!result)
        //             Assert.Fail ();
        //         var ping = Encoding.ASCII.GetBytes ("Ping");
        //         rUDPClient.SendToAll (Channel.None, ping);
        //         rUDPClient.SendTo (serverSocket.EndPoint, Channel.None, ping);
        //     }
        // }

        [Test]
        public void TestClientCanConnectAndDisconnect ()
        {
             using (var rUDPServer = new RUDP<MockUDPSocket>(serverSocket)) {
                using (var rUDPClient = new RUDP<MockUDPSocket>(clientSocket)) {
                    rUDPServer.Start (8000);
                    rUDPClient.Start (8001);
                    rUDPClient.Connect (serverAny.Address.ToString (), 8000);
                    Thread.Sleep (100);
                    Assert.AreEqual (1, rUDPServer.Remotes.Length);
                    Assert.AreEqual (1, rUDPClient.Remotes.Length);

                    rUDPClient.Disconnect ();
                    Thread.Sleep (100);
                    Assert.AreEqual (0, rUDPServer.Remotes.Length);
                    Assert.AreEqual (0, rUDPClient.Remotes.Length);
                }
             }
        }

        [Test]
        public async Task TestMockSocketLink ()
        {
            serverSocket.Initialize ();
            serverSocket.Listen (serverAny.Port);
            clientSocket.Initialize ();
            clientSocket.Listen (clientAny.Port);
            var ping = Encoding.ASCII.GetBytes ("Ping");
            Assert.IsTrue (await clientSocket.SendTo (serverSocket.EndPoint, ping, default));
            
            var data = serverSocket.RecievedPackets.Take ();
            Assert.AreEqual (clientSocket.EndPoint, data.remote);
            Assert.AreEqual (ping.Length, data.data.Length);
            Assert.AreEqual (ping, data.data, $"({(string.Join (",", data.data))}) != ({(string.Join (",", ping))})");

            var pong = Encoding.ASCII.GetBytes ("Pong");
            Assert.IsTrue (await serverSocket.SendTo (clientSocket.EndPoint, pong, default));
            data = clientSocket.RecievedPackets.Take ();
            Assert.AreEqual (serverSocket.EndPoint, data.remote);
            Assert.AreEqual (pong.Length, data.data.Length);
            Assert.AreEqual (pong, data.data, $"({(string.Join (",", data.data))}) != ({(string.Join (",", pong))})");
            serverSocket.Dispose ();
            clientSocket.Dispose ();
        }

        [Test]
        public void CheckMessagesAreSent ()
        {
            using (var rUDPServer = new RUDP<MockUDPSocket>(serverSocket)) {
                using (var rUDPClient = new RUDP<MockUDPSocket>(clientSocket)) {
                    EndPoint remote = null;
                    byte[] dataReceived = null;
                    var wait = new ManualResetEvent (false);
                    rUDPServer.ConnetionRequested += (EndPoint e, byte[] data) => {
                        return true;
                    };
                    rUDPServer.DataReceived = (EndPoint e, byte[] data) => {
                        wait.Set ();
                        remote = e;
                        dataReceived = data;
                        return true;
                    };
                    rUDPClient.ConnetionRequested += (EndPoint e, byte[] data) => {
                        return true;
                    };
                    rUDPClient.DataReceived = (EndPoint e, byte [] data) => {
                        wait.Set ();
                        remote = e;
                        dataReceived = data;
                        return true;
                    };
                    rUDPServer.Start (8000);
                    rUDPClient.Start (8001);
                    Assert.IsTrue (rUDPClient.Connect (serverAny.Address.ToString (), 8000));
                    wait.WaitOne (500);
                    Assert.AreEqual(1, rUDPServer.Remotes.Length);
                    Assert.AreEqual (rUDPClient.EndPoint, rUDPServer.Remotes[0]);

                    Assert.AreEqual(1, rUDPClient.Remotes.Length);
                    Assert.AreEqual (rUDPServer.EndPoint, rUDPClient.Remotes[0]);

                    byte[] message = Encoding.ASCII.GetBytes ("Ping");
                    Assert.IsTrue (rUDPClient.SendTo (rUDPServer.EndPoint, Channel.None, message));
                    wait.WaitOne (5000);

                    Assert.AreEqual (message, dataReceived, $"({(string.Join (",", dataReceived ?? new byte[0]))}) != ({(string.Join (",", message))})");
                    Assert.AreEqual (rUDPClient.EndPoint, remote);

                    message = Encoding.ASCII.GetBytes ("Pong");
                    dataReceived = null;
                    remote = null;
                    wait.Reset ();
                    Assert.IsTrue (rUDPServer.SendToAll (Channel.None, message));

                    wait.WaitOne (5000);

                    Assert.AreEqual (message, dataReceived, $"({(string.Join (",", dataReceived ?? new byte[0]))}) != ({(string.Join (",", message))})");
                    Assert.AreEqual (rUDPServer.EndPoint, remote);

                    Assert.IsTrue (rUDPClient.Disconnect ());
                    Thread.Sleep (100);
                    Assert.AreEqual (0, rUDPServer.Remotes.Length);
                    Assert.AreEqual (0, rUDPClient.Remotes.Length);
                }
            }
        }
    }
}