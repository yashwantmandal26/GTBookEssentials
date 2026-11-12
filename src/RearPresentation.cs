using System;
namespace GTBook {
    public sealed class RearPresentation {
        public bool? PowerOn {get;private set;}
        public bool PreviewDraft {get;set;}
        public bool HasEdits {get;private set;}
        public string Failure {get;private set;}
        public bool IsStarting {get;private set;}
        public bool MusicActive {get{return !IsStarting&&PowerOn==true&&applied!=null&&applied.Action=="rear-music";}}
        Request applied;
        static Request Clone(Request r){return new Request{Action=r.Action,Mode=r.Mode,Effect=r.Effect,Colors=(string[])r.Colors.Clone(),Brightness=r.Brightness,Speed=r.Speed,Enabled=r.Enabled,KeepAwake=r.KeepAwake,ZoneEffect=r.ZoneEffect,Step=r.Step};}
        public void Edited(){HasEdits=true;}
        public void Edited(Request draft){HasEdits=applied==null||!RearLighting.SameLook(applied,draft);}
        public void Applied(Request r){IsStarting=false;PreviewDraft=false;HasEdits=false;Failure=null;PowerOn=r.Action!="rear-off"&&r.Brightness>0;applied=Clone(r);}
        public void Failed(){IsStarting=false;PowerOn=null;applied=null;PreviewDraft=false;Failure="Not confirmed";}
        public void Starting(){IsStarting=true;PowerOn=null;applied=null;PreviewDraft=false;Failure=null;}
        public void ObservePower(bool on){if(PowerOn!=on){applied=null;PreviewDraft=false;}PowerOn=on;Failure=null;}
        public Request Illustration(Request draft){
            var selected=Clone(!PreviewDraft&&applied!=null?applied:draft);
            if(!PreviewDraft&&(PowerOn!=true||Failure!=null)){selected.Action="rear-off";selected.Brightness=0;}
            return selected;
        }
        public string Badge {get{return PreviewDraft?"PREVIEW":IsStarting?"STARTING":Failure!=null||PowerOn==null?"UNKNOWN":PowerOn==false?"OFF":MusicActive?"MUSIC":"ON";}}
        public string Caption(Request draft){
            if(IsStarting)return "STARTING · waiting for the rear controller";
            if(PreviewDraft)return (PowerOn==false?"DEVICE OFF":"SELECTED LOOK")+" · preview only · not applied";
            if(Failure!=null)return "NOT CONFIRMED · use Check status to read device power";
            if(PowerOn==false)return HasEdits?"OFF · changes selected, not applied":"OFF · rear light disabled";
            if(applied!=null)return (MusicActive?"MUSIC · ":"ON · ")+applied.Effect+(HasEdits?" · changes selected, not applied":" · controller acknowledged");
            if(PowerOn==true)return HasEdits?"ON · showing draft · Apply to update":"ON · selected-effect simulation";
            return "UNKNOWN · use Check status to read device power";
        }
    }
}
