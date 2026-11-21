namespace CardGame.Core
{
    /// <summary>Accepts one stable physical marker, then requires fresh empty frames.</summary>
    public sealed class MultiCardScanGate
    {
        private const float FreshGap = .25f, HoldTime = .55f, ClearTime = .7f;
        private int candidate = -1;
        private float firstSeen, lastSeen, clearStart = -1, lastClear;
        public bool WaitingForClear { get; private set; } = true;
        public void Lock()
        { WaitingForClear=true; candidate=-1; clearStart=-1; }
        public void Lost(){candidate=-1;}
        public bool Observe(int id,float now,bool allowed)
        {
            clearStart=-1;
            if(!allowed){Lock();return false;}
            if(WaitingForClear||id<0||id>49){candidate=-1;return false;}
            if(candidate!=id||now-lastSeen>FreshGap){candidate=id;firstSeen=now;}
            lastSeen=now;
            if(now-firstSeen<HoldTime)return false;
            Lock();return true;
        }
        public void Frame(int count,float now)
        {
            if(count!=0){clearStart=-1;if(count!=1)candidate=-1;return;}
            candidate=-1;
            if(!WaitingForClear)return;
            if(clearStart<0||now-lastClear>FreshGap)clearStart=now;
            lastClear=now;
            if(now-clearStart>=ClearTime){WaitingForClear=false;clearStart=-1;}
        }
    }
}
