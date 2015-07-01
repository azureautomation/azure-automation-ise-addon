using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using Microsoft.Azure.Management.Automation.Models;

namespace AutomationISE.Model
{
    public class LocalRunbook
    {
        /* The runbook object which exists in Azure Automation, and is referred to by this local object */
        public Runbook cloudRunbook { get; set; }
        /* The location of the .ps1 workflow file on disk */
        //public File runbookFile { get; set; }

        public LocalRunbook()
        {
            this.cloudRunbook = null;
            //this.runbookFile = null;
        }
        public LocalRunbook(Runbook cloudRunbook)
        {
            this.cloudRunbook = cloudRunbook;
            //this.runbookFile = null;
        }
    }
}
