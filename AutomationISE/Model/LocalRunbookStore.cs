using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Azure.Management.Automation.Models;

namespace AutomationISE.Model
{
    public class LocalRunbookStore
    {
        public IList<LocalRunbook> localRunbooks { get; set; }
        public LocalRunbookStore()
        {
            localRunbooks = null;
        }

        public void UpdateLocalRunbooks(IList<Runbook> cloudRunbooks)
        {
            localRunbooks = new List<LocalRunbook>();
            foreach (Runbook cloudRunbook in cloudRunbooks)
            {
                localRunbooks.Add(new LocalRunbook(cloudRunbook));
            }
        }
    }
}
