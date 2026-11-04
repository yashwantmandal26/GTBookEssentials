using System;
namespace GTBook {
    public sealed class RearPresentation {
        public bool? PowerOn {get;private set;}
        public bool PreviewDraft {get;set;}
        public bool HasEdits {get;private set;}
        public string Failure {get;private set;}
        Request applied;
        static Request Clone(Request r){return new Request{Action=r.Action,Mode=r.Mode,Effect=r.Effect,Colors=(string[])r.Colors.Clone(),Brightness=r.Brightness,Speed=r.Speed,Enabled=r.Enabled,KeepAwake=r.KeepAwake,ZoneEffect=r.ZoneEffect,Step=r.Step};}
        public void Edited(){HasEdits=true;}
        public void Applied(Request r){PreviewDraft=false;HasEdits=false;Failure=null;PowerOn=r.Action!="rear-off";applied=Clone(r);}
        public void Failed(){PowerOn=null;applied=null;PreviewDraft=false;Failure="Not confirmed";}
        public void Starting(){PowerOn=null;applied=null;PreviewDraft=true;Failure=null;}
        public void ObservePower(bool on){if(PowerOn!=on){applied=null;PreviewDraft=false;}PowerOn=on;Failure=null;}
        public Request Illustration(Request draft){
            var selected=Clone(!PreviewDraft&&!HasEdits&&applied!=null?applied:draft);
            if(!PreviewDraft&&(PowerOn==false||Failure!=null)){selected.Action="rear-off";selected.Brightness=0;}
            return selected;
        }
        public string Caption(Request draft){
            if(PreviewDraft)return (PowerOn==false?"DEVICE OFF":"SELECTED LOOK")+" · preview only · not applied";
            if(Failure!=null)return "NOT CONFIRMED · Refresh to check the rear light";
            if(PowerOn==false)return HasEdits?"OFF · changes selected, not applied":"OFF · rear light disabled";
            if(applied!=null&&!HasEdits)return applied.Brightness==0?"OFF · brightness is 0%":"ON · "+applied.Effect+" · command sent";
            if(PowerOn==true)return HasEdits?"ON · showing draft · Apply to update":"ON · selected-effect simulation";
            return "PREVIEW ONLY · device state not read";
        }
    }
}
