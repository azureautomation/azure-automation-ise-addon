using System;
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
    }
}
