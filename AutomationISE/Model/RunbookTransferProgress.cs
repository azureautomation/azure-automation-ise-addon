using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutomationISE.Model
{
    public class RunbookTransferProgress
    {
        public RunbookTransferJob Job { get; set; }
        public TransferStatus Status;
        public RunbookTransferProgress(RunbookTransferJob rtj)
        {
            this.Job = rtj;
            this.Status = TransferStatus.Starting;
        }
        public enum TransferStatus
        {
            Starting,
            Completed,
            AllCompleted
        };
    }
}
