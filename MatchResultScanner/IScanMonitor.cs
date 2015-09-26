using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchResultScanner
{
    public delegate void OnScannerStartHandler(MatchScanner scanner);
    public delegate void OnScannerEndHandler(MatchScanner scanner, bool scanIsTotallyFinished, bool scanHasNoMoreWork);

    public interface IScanMonitor
    {
        event OnScannerStartHandler OnScannerStart;
        event OnScannerEndHandler OnScannerEnd;

        void Start();
        void Stop();
    }
}
