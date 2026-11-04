using System;
using System.Collections.Generic;
using System.Linq;

namespace GTBook {
    public sealed class ConflictCache {
        string fingerprint=null,message="";
        public string Check(IEnumerable<string> identities,Func<string> inspect){
            string next=string.Join("|",identities.OrderBy(x=>x,StringComparer.Ordinal));
            if(next.Length==0){fingerprint=next;message="";return "";}
            if(next!=fingerprint){string result=inspect();fingerprint=next;message=result;}
            return message;
        }
    }
}
