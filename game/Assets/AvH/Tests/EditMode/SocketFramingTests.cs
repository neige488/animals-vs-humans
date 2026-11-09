using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
namespace AvH.Tests {
 public class SocketFramingTests {
  static byte[] Packet(params object[] values){
   var type=typeof(PrivateRoomClient).Assembly.GetType("AvH.RoomWire");var bytes=(byte[])type.GetMethod("Encode",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{values});
   var packet=new byte[bytes.Length+4];Array.Copy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(bytes.Length)),packet,4);Array.Copy(bytes,0,packet,4,bytes.Length);return packet;
  }
  [Test] public void FragmentedFramesAndCoalescedBurstsSurviveReceiveBufferCompaction(){
   var world=new PlaytestSession(2);world.StartSolo("host");int seen=0;
   using(var host=new PrivateRoomHost(world,"127.0.0.1")){
    host.InputReceived=(slot,input)=>{if(input.Sequence>0)seen=input.Sequence;};
    PrivateRoomCode.TryParse(host.RoomCode,out var address,out int port,out var key);
    using(var raw=new TcpClient(address,port)){raw.NoDelay=true;var stream=raw.GetStream();
     var hello=Packet("hello",RoomProtocol.Version,key,new string('a',32),"fragmented");
     for(int offset=0;offset<hello.Length;){int count=Math.Min(offset%7+1,hello.Length-offset);stream.Write(hello,offset,count);offset+=count;host.Pump(false);Thread.Sleep(1);}
     Assert.AreEqual(1,world.Observe().Players.Count(p=>!p.IsBot),"A fragmented hello alone cannot claim a slot");
     var ready=Packet("ready");stream.Write(ready,0,ready.Length);for(int i=0;i<20;i++){host.Pump(false);Thread.Sleep(1);}
     Assert.AreEqual(2,world.Observe().Players.Count(p=>!p.IsBot));
     // More than a receive's 32-frame budget plus a trailing partial header must be preserved across pumps.
     var ping=Packet("ping");var input=Packet("input",new NetworkInput{RoundId=world.Round,Sequence=123,Forward=1});
     using(var burst=new MemoryStream()){for(int i=0;i<80;i++)burst.Write(ping,0,ping.Length);burst.Write(input,0,2);var bytes=burst.ToArray();stream.Write(bytes,0,bytes.Length);}
     for(int i=0;i<10;i++){host.Pump(false);Thread.Sleep(1);}Assert.AreEqual(0,seen);
     stream.Write(input,2,input.Length-2);for(int i=0;i<20&&seen==0;i++){host.Pump(false);Thread.Sleep(1);}Assert.AreEqual(123,seen);
    }
   }
  }
 }
}
