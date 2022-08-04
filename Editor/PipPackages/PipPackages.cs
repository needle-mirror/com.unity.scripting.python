using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;
using UnityEngine;
using Python.Runtime;

[assembly:System.Runtime.CompilerServices.InternalsVisibleTo("Unity.Scripting.Python.Tests")]

namespace UnityEditor.Scripting.Python.Packages
{
    internal class PipPackages
    {

        private static readonly string PipPath = Path.GetFullPath("Packages/com.unity.scripting.python/Editor/PipPackages");
        private static readonly string updatePackagesScript = Path.Combine(PipPath, "update_packages.py");
        private static readonly string compiledRequirementsPath = $"{Directory.GetCurrentDirectory()}/Temp/compiled_requirements.txt";

        static void ProgressBarHelper (dynamic process, string title, string info)
        {
                float progress = 0.25f;
                bool reverse = false;
                while (process.poll() == null)
                {
                    if (!reverse)
                    {
                        // progress bar "grows"
                        progress += 0.01f;
                    }
                    else
                    {
                        // progress bar shrinks
                        progress -= 0.01f;
                    }
                    if (progress > 0.85f)
                    {
                        // we've reached the "max growth", now shrink
                        reverse = true;
                    }
                    else if (progress < 0.15f)
                    {
                        // we've reached the "max shrinkage", now grow.
                        reverse = false;
                    }
                    EditorUtility.DisplayProgressBar(title, info, progress);
                    
                    // sleep for about a frame
                    Thread.Sleep(17/*ms*/);
                }
                EditorUtility.ClearProgressBar();
        }

        /// <summary>
        /// Compiles the full requirements (dependencies included) of a given
        /// requirements file.
        /// 
        /// Returns the process' retcode.
        /// </summary>
        static int CompileRequirements(string requirementsFile, string pythonInterpreter)
        {
            PythonRunner.EnsureInitialized();
            using (Py.GIL())
            {
                dynamic spawn_process = Py.Import("unity_python.common.spawn_process");
                dynamic args = new PyList();
                args.append("-m");
                args.append("piptools");
                args.append("compile");
                args.append("-o");
                args.append(compiledRequirementsPath);
                args.append(requirementsFile);

                dynamic subprocess = Py.Import("subprocess");
                var subprocessKwargs = new PyDict();
                subprocessKwargs["stdout"] = subprocess.PIPE;
                subprocessKwargs["stderr"] = subprocess.PIPE;
                subprocessKwargs["universal_newlines"] = true.ToPython();
                dynamic process = spawn_process.spawn_process_in_environment(pythonInterpreter, args, kwargs: subprocessKwargs);

                ProgressBarHelper(process, "Compiling requirements", "Pip requirements compilation in progress");
                // read stdin/out until EOF
                dynamic res = process.communicate();
                // get the retcode after process has finished
                int retcode = process.poll();
                (string output, string errors) = (res[0], res[1]);
                // inform the user only on failure, the pip install will inform
                // the user of the installed packages
                if(retcode != 0)
                {
                    var strbuilder = new StringBuilder();
                    strbuilder.AppendLine("Error while compiling requirements:");
                    foreach (var line in Regex.Split(errors, "\r\n|\n|\r"))
                    {
                        if (!line.StartsWith("#"))
                        {
                            strbuilder.AppendLine(line);
                        }
                    }
                    Debug.LogError(strbuilder.ToString());
                }
                return retcode;
            }
        }

        /// <summary>
        /// Execute the Python script `update_packages.py`: using a package requirements.txt file, it will install
        /// needed packages and uninstall unused ones
        /// </summary>
        /// <param="requirementsFile">Path to the requirements.txt file</param>
        /// <param="pythonInterpreter">Path to the Python interpreter on wich we run the update packages script</param>
        /// <returns>Standard output of the script</returns>
        private static string UpdatePackagesHelper(string requirementsFile,
                                                    string pythonInterpreter)
        {
            PythonRunner.EnsureInitialized();
            using (Py.GIL())
            {
                dynamic spawn_process = Py.Import("unity_python.common.spawn_process");

                dynamic args = new PyList();
                args.append(updatePackagesScript);
                args.append(compiledRequirementsPath);
                // Only take packages in our site-packages, don't pick up the ones installed on the system.
                args.append(Path.GetFullPath(PythonSettings.kSitePackagesRelativePath));

                dynamic subprocess = Py.Import("subprocess");
                var subprocessKwargs = new PyDict();
                subprocessKwargs["stdout"] = subprocess.PIPE;
                subprocessKwargs["stderr"] = subprocess.PIPE;
                subprocessKwargs["universal_newlines"] = true.ToPython();

                dynamic process = spawn_process.spawn_process_in_environment(pythonInterpreter, args,
                                                              kwargs: subprocessKwargs,
                                                              wantLogging: false);
                ProgressBarHelper(process, "Updating required pip packages", "This could take a few minutes");
  
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

        internal static string UpdatePackages(string requirementsFile,
                                              string pythonInterpreter = PythonSettings.kDefaultPython)
        {
            PythonRunner.EnsureInitialized();
            using (Py.GIL())
            {
                // As piptools sync is made to work only with requirements that have been
                // gemerated by piptools compile, use their workflow: compile the
                // user-supplied requirements, which may or may not contain dependents.
                if (CompileRequirements(requirementsFile, pythonInterpreter) != 0)
                {
                    return string.Empty;
                }
                return UpdatePackagesHelper(requirementsFile, pythonInterpreter);
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
            
            const string notOnPath = @"WARNING:.+ installed in.+ which is not on PATH\.";
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
