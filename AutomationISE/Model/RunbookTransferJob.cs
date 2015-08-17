using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutomationISE.Model
{
    public class RunbookTransferJob
    {
        public AutomationRunbook Runbook { get; set; }
        public TransferOperation Operation { get; set; }
        public RunbookTransferJob(AutomationRunbook rb, TransferOperation t)
        {
            this.Runbook = rb;
            this.Operation = t;
        }
        public enum TransferOperation
        {
            Download,
            Upload
        };
    }
}
