using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

using Python.Runtime;

[assembly:System.Runtime.CompilerServices.InternalsVisibleTo("Unity.Scripting.Python.Tests")]

namespace UnityEditor.Scripting.Python.Packages
{
    internal class PipPackages
    {

        private static readonly string PipPath = Path.GetFullPath("Packages/com.unity.scripting.python/Editor/PipPackages");
        private static readonly string updatePackagesScript = Path.Combine(PipPath, "update_packages.py");

        /// <summary>
        /// Execute the Python script `update_packages.py`: using a package requirements.txt file, it will install
        /// needed packages and uninstall unused ones
        /// </summary>
        /// <param="requirementsFile">Path to the requirements.txt file</param>
        /// <param="pythonInterpreter">Path to the Python interpreter on wich we run the update packages script</param>
        /// <returns>Standard output of the script</returns>
        internal static string UpdatePackages(string requirementsFile,
                                            string pythonInterpreter = PythonSettings.kDefaultPython)
        {
            PythonRunner.EnsureInitialized();
            using (Py.GIL())
            {
                dynamic spawn_process = PythonEngine.ImportModule("unity_python.common.spawn_process");

                dynamic args = new PyList();
                args.append(updatePackagesScript);
                args.append(requirementsFile);

                dynamic subprocess = PythonEngine.ImportModule("subprocess");
                var subprocessKwargs = new PyDict();
                subprocessKwargs["stdout"] = subprocess.PIPE;
                subprocessKwargs["stderr"] = subprocess.PIPE;
                subprocessKwargs["universal_newlines"] = true.ToPython();

                dynamic process = spawn_process.spawn_process_in_environment(pythonInterpreter, args,
                                                              kwargs: subprocessKwargs,
                                                              wantLogging: false);
                dynamic res = process.communicate();
                (string output, string errors) = (res[0], res[1]);
                if (!string.IsNullOrEmpty(errors))
                {
                    var pipWarningStringBuilder = new StringBuilder();
                    // Split errors lines to filter them individually
                    foreach (var line in Regex.Split(errors, "\r\n|\n|\r"))
                    {
                        if (IsInterestingWarning(line))
                        {
                            pipWarningStringBuilder.AppendLine(line);
                        }
                    }

                    if (pipWarningStringBuilder.Length > 0)
                    {
                        UnityEngine.Debug.LogError(pipWarningStringBuilder.ToString());
                    }
                }                    

                return output;
            }
        }

        /// <summary>
        /// Returns true if the warning is interesting. Use this to filter output from pip.
        /// Certain common warnings from pip are uninteresting.
        /// Returns false if the warning is empty.
        /// </summary>
        internal static bool IsInterestingWarning(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false; // message is not a pip warning, but there is no point to display it
            }
            
            const string notOnPath = @"WARNING:.+ are installed in.+ which is not on PATH\.";
            const string considerAddingToPath = "Consider adding this directory to PATH";
            const string newPipVersionAvailable = @"WARNING: You are using pip version \d+\.\d+(.\d+)?;.+version \d+\.\d+(.\d+)? is available\.";
            const string considerPipUpgrade = @"You should consider upgrading via the.+-m pip install --upgrade pip' command\.";
            
            string[] patternsToFilterOut = {notOnPath, considerAddingToPath, newPipVersionAvailable, considerPipUpgrade};

            int anyMatch = patternsToFilterOut.Select(pattern => Regex.IsMatch(message, pattern))
                .Where(match => match).Count();

            return anyMatch == 0;
        }
    }
}
