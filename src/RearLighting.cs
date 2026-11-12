using System;
using System.IO;

namespace GTBook {
    public static class RearLighting {
        public static readonly string[] Names={"Static","Breathing","Loop","Cover","Rhythm","Beat"};
        public static bool UsesSecondColour(string effect){return effect=="Loop"||effect=="Cover";}
        public static bool IsMusic(string effect){return effect=="Rhythm"||effect=="Beat";}
        public static bool SameLook(Request a,Request b){return a.Effect==b.Effect&&a.Brightness==b.Brightness&&a.Colors[0]==b.Colors[0]&&(!UsesSecondColour(a.Effect)||a.Colors[1]==b.Colors[1])&&(a.Effect!="Breathing"||a.Speed==b.Speed);}
        public static string Description(string effect){
            switch(effect){
                case "Static":return "One steady colour. Runs on the rear controller.";
                case "Breathing":return "A soft colour pulse. Adjust the breathing speed below.";
                case "Loop":return "Two colours cycle along the bar. Runs on the rear controller.";
                case "Cover":return "Two colours sweep across the bar. Runs on the rear controller.";
                case "Rhythm":return "Reacts to playback volume. Keep Essentials open and play audio.";
                default:return "Pulses with playback volume. Keep Essentials open and play audio.";
            }
        }
        public static byte[] Frame(Request r){
            var packet=Protocol.Rear(r.Effect,r.Colors[0],r.Brightness,r.Speed);
            if(UsesSecondColour(r.Effect)){Array.Copy(Protocol.Color(r.Colors[1]),0,packet,12,3);packet[15]=packet[10];}
            return packet;
        }
        // The gate can turn the bar off even if its UART is missing or busy.
        public static void Power(bool enabled,Action<bool> write,Func<bool> read,Action<int> wait){
            write(enabled);
            for(int i=0;i<8;i++){if(i>0)wait(80);if(read()==enabled)return;}
            throw new IOException("Rear power-"+(enabled?"on":"off")+" read-back did not match. Check the device state before retrying.");
        }
        public static bool StopsMusic(string action){return action=="rear"||action=="rear-off";}
    }
    public static class RearMusic {
        public const string ReadyName=@"Local\GTBookRearMusicReady";
        public static int Stream(Func<bool> stopping,Func<bool> ownerAlive,Func<double> peak,Action<byte> send,Action ready,Action wait){
            int frames=0;
            while(!stopping()&&ownerAlive()){
                send((byte)Math.Round(Math.Max(0,Math.Min(1,peak()))*255));
                if(frames++==0)ready();wait();
            }
            return frames;
        }
    }
}
