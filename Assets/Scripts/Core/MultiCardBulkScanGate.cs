using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core
{
    /// <summary>Independent stable IDs, fresh analyzed frames only; no repeat until removed.</summary>
    public sealed class MultiCardBulkScanGate
    {
        private const float FreshGap=.25f, Hold=.4f, Clear=.4f;
        private readonly Dictionary<int,float> first=new Dictionary<int,float>();
        private readonly HashSet<int> consumed=new HashSet<int>();
        private float last=-1,clearStart=-1;
        public bool WaitingForClear { get; private set; }=true;
        public void Reset(){first.Clear();consumed.Clear();last=-1;clearStart=-1;WaitingForClear=true;}
        public IReadOnlyList<int> Frame(IReadOnlyList<int> ids,float now)
        {
            if(last>=0&&(now<last||now-last>FreshGap))Reset();
            last=now;
            var visible=new HashSet<int>(ids.Where(id=>id>=0&&id<50));
            var ready=new List<int>();
            if(WaitingForClear){
                if(ids.Count!=0){clearStart=-1;return ready;}
                if(clearStart<0)clearStart=now;
                if(now-clearStart>=Clear)WaitingForClear=false;
                return ready;
            }
            foreach(int id in first.Keys.Where(id=>!visible.Contains(id)).ToArray()){first.Remove(id);consumed.Remove(id);}
            foreach(int id in visible.OrderBy(id=>id)){
                if(!first.ContainsKey(id))first[id]=now;
                if(!consumed.Contains(id)&&now-first[id]>=Hold){ready.Add(id);consumed.Add(id);}
            }
            return ready;
        }
    }
}
