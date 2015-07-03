﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using Microsoft.Azure.Management.Automation.Models;

namespace AutomationISE.Model
{
    /* 
     * Returns runbooks currently stored on disk. 
     * Used by the AutomationRunbookManager to compare cloud runbooks with ones already saved.
     */
    public class LocalRunbookStore
    {
        public IList<AutomationRunbook> localRunbooks { get; set; }
        public LocalRunbookStore()
        {
            localRunbooks = null;
        }

        public LocalRunbookStore(string workspace)
        {
            localRunbooks = new List<AutomationRunbook>();
            /* scan the workspace, populate localRunbooks with what you find */
        }

        public static ISet<string> GetLocalRunbookPaths(string workspace)
        {
            if (!Directory.Exists(workspace)) return null;
            ISet<string> filePathsSet = new HashSet<string>();
            string[] localRunbookFilePaths = Directory.GetFiles(workspace, "*.ps1");
            foreach(string path in localRunbookFilePaths)
            {
                filePathsSet.Add(path);
            }
            return filePathsSet;
        }


        /* TODO: May not be useful if it's better to throw out and recreate the object */
        public void Update(IList<Runbook> cloudRunbooks)
        {
            localRunbooks = new List<AutomationRunbook>();
            foreach (Runbook cloudRunbook in cloudRunbooks)
            {
                localRunbooks.Add(new AutomationRunbook(cloudRunbook));
            }
        }
    }
}
