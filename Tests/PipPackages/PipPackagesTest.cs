using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;

using NUnit.Framework;

using Python.Runtime;
using UnityEditor.Scripting.Python;
using UnityEditor.Scripting.Python.Packages;
using static UnityEditor.Scripting.Python.Tests.PythonTestUtils;


namespace UnityEditor.Scripting.Python.Tests
{
    internal class PipPackagesTest : PipTestBase
    {

    internal static string TestsPath = Path.Combine(Path.GetFullPath("Packages/com.unity.scripting.python"),
                                                "Tests", "PipPackages");


    // the function unity_python.common.spawn_process.spawn_process_in_environment
    dynamic spawn;

    [SetUp]
    public void Setup()
    {
        PythonRunner.EnsureInitialized();
        using (Py.GIL())
        {
            dynamic module = PythonEngine.ImportModule("unity_python.common.spawn_process");
            spawn = module.spawn_process_in_environment;
        }
    }

    [TearDown]
    public void Teardown()
    {
        spawn = null;
    }

    /// <summary>
    /// Spawns a `python -m pip freeze` subprocess and returns its output
    /// </summary>
    /// <param="pythonInterpreter">Path to the Python interpreter on which we call pip freeze</param>
    /// <param="pythonPath">Override PYTHONPATH with the passed argument; no override if empty string</param>
    /// <returns>Standard output of the pip freeze subprocess</returns>
    internal static string CallPipFreeze(string pythonInterpreter = PythonSettings.kDefaultPython,
                                        string pythonPath = "")
    {
        PythonRunner.EnsureInitialized();
        using (Py.GIL())
        {
            dynamic subprocess = PythonEngine.ImportModule("subprocess");
            dynamic module = PythonEngine.ImportModule("unity_python.common.spawn_process");
            dynamic spawn = module.spawn_process_in_environment;

            dynamic args = new PyList();
            args.append(new PyString("-m"));
            args.append(new PyString("pip"));
            args.append(new PyString("freeze"));
            var subprocessKwargs = new PyDict();
            subprocessKwargs["stdout"] = subprocess.PIPE;
            subprocessKwargs["stderr"] = subprocess.PIPE;
            subprocessKwargs["universal_newlines"] = true.ToPython(); // text streams instead of bytes

            dynamic envOverride = new PyDict();
            if (string.IsNullOrEmpty(pythonPath))
            {
                envOverride["PYTHONPATH"] = new PyString("");
            }

            dynamic process = spawn(pythonInterpreter, args,
                    kwargs: subprocessKwargs,
                    env_override: envOverride,
                    wantLogging: false);
            dynamic res = process.communicate();
            (string output, string errors) = (res[0], res[1]);
            if (errors != null && errors.Length > 0)
            {
                UnityEngine.Debug.LogError(errors);
            }

            return output;
        }
    }

    [UnityTest]
    public IEnumerator TestUpdatePackagesWithOneUpgrade()
    {
        using (Py.GIL())
        {
        dynamic shlex = PythonEngine.ImportModule("shlex");

        using (var env = new PyTestVenv())
        {
#if UNITY_EDITOR_WIN
            string  pythonVenvInterpreter = Path.Combine(env.path, "Scripts", "python.exe");
            // CallPipFreeze, PYTHONPATH limited to py venv path
            string pythonPath = Path.Combine(env.path, "Lib", "site-packages");
#else
            string  pythonVenvInterpreter = Path.Combine(env.path, "bin", "python3");
            string pythonPath = Path.Combine(env.path, "lib", "site-packages", "python3.7", "site-packages");
#endif
            // Install toml 0.9.0 into the py venv
            var argsStr = new PyString("-m pip install toml==0.9.0");
            var args = shlex.split(argsStr);
            dynamic proc = spawn(pythonVenvInterpreter, args,
                    wantLogging: false);
            yield return WaitForProcessEnd(proc, 20);

            // Need to update pip, piptools requires pip._internal.commands.create_command, which is not available witout updating
            argsStr = new PyString(" -m pip install pip -U");
            args = shlex.split(argsStr);
            dynamic envOverride = new PyDict();
            envOverride["PYTHONPATH"] = new PyString(pythonPath);
            dynamic proc2 = spawn(pythonVenvInterpreter, args,
                    env_override: envOverride,
                    wantLogging: false);
            yield return WaitForProcessEnd(proc2, 20);

            // Call UpdatePackages with a requirements.txt file containing only a requirement to toml 0.10.0
            var testRequirementFile = Path.Combine(TestsPath, "test_requirements_1.txt");
            PipPackages.UpdatePackages(testRequirementFile, pythonVenvInterpreter);

            // NOTE:
            // in this case, this not pip-tools diff/sync funcs that remove toml 9 for toml 10,
            // it is pip that sees another version of the same package exists and then uninstall
            // the current version before installing the required one

            // Check that the only package in the py venv is toml 0.10.0
            var output = CallPipFreeze(pythonVenvInterpreter, pythonPath);
            Assert.That(output, Is.EqualTo("toml==0.10.0\n"));
        }
        }
    }

    [UnityTest]
    public IEnumerator TestUpdatePackagesWithOneDowngrade()
    {
        using (Py.GIL())
        {
        dynamic shlex = PythonEngine.ImportModule("shlex");

        using (var env = new PyTestVenv())
        {
#if UNITY_EDITOR_WIN
            string  pythonVenvInterpreter = Path.Combine(env.path, "Scripts", "python.exe");
            // CallPipFreeze, PYTHONPATH limited to py venv path
            string pythonPath = Path.Combine(env.path, "Lib", "site-packages");
#else
            string  pythonVenvInterpreter = Path.Combine(env.path, "bin", "python3");
            string pythonPath = Path.Combine(env.path, "lib", "site-packages", "python3.7", "site-packages");
#endif
            // Install toml 0.10.0 into the py venv
            var argsStr = new PyString("-m pip install toml==0.10.0");
            var args = shlex.split(argsStr);
            dynamic proc = spawn(pythonVenvInterpreter, args,
                    wantLogging: false);
            yield return WaitForProcessEnd(proc, 20);

            argsStr = new PyString(" -m pip install pip -U");
            args = shlex.split(argsStr);
            dynamic envOverride = new PyDict();
            envOverride["PYTHONPATH"] = new PyString(pythonPath);
            dynamic proc2 = spawn(pythonVenvInterpreter, args,
                    env_override: envOverride,
                    wantLogging: false);
            yield return WaitForProcessEnd(proc2, 20);

            // Call UpdatePackages with a requirements.txt file containing only a requirement to toml 0.9.0
            var testRequirementFile = Path.Combine(TestsPath, "test_requirements_2.txt");
            PipPackages.UpdatePackages(testRequirementFile, pythonVenvInterpreter);

            // Check that the only package in the py venv is toml 0.9.0

            var output = CallPipFreeze(pythonVenvInterpreter, pythonPath);
            Assert.That(output, Is.EqualTo("toml==0.9.0\n"));
        }
        }
    }

    [Ignore("TODO: get this test to be reliable cross-platform by controlling pip (UT-3692)")]
    [UnityTest]
    public IEnumerator TestUpdatePackagesWithSeveralPackages()
    {
        using (Py.GIL())
        {
        dynamic shlex = PythonEngine.ImportModule("shlex");

        using (var env = new PyTestVenv())
        {
#if UNITY_EDITOR_WIN
            string  pythonVenvInterpreter = Path.Combine(env.path, "Scripts", "python.exe");
            // CallPipFreeze, PYTHONPATH limited to py venv path
            string pythonPath = Path.Combine(env.path, "Lib", "site-packages");
#else
            string  pythonVenvInterpreter = Path.Combine(env.path, "bin", "python3");
            string pythonPath = Path.Combine(env.path, "lib", "site-packages", "python3.7", "site-packages");
#endif
            // Install several packages:
            // numpy & vg have no dependencies
            // UnityPy depends on Brotli colorama lz4 Pillow termcolor
            var argsStr = new PyString("-m pip install numpy==1.17.5  vg==1.6.0 UnityPy==1.2.4.8");
            var args = shlex.split(argsStr);
            dynamic proc = spawn(pythonVenvInterpreter, args,
                    wantLogging: false);
            yield return WaitForProcessEnd(proc, 60);

            // Check installations went as expected, to ensure our test is properly set
            string output = CallPipFreeze(pythonVenvInterpreter, pythonPath);
            // requested packages with specific versions
            Assert.That(output, Contains.Substring("numpy==1.17.5"));
            Assert.That(output, Contains.Substring("vg==1.6.0"));
            Assert.That(output, Contains.Substring("UnityPy==1.2.4.8"));
            // dependent packages, we don't know the version number
            Assert.That(output, Contains.Substring("Brotli"));
            Assert.That(output, Contains.Substring("colorama"));
            Assert.That(output, Contains.Substring("lz4"));
            Assert.That(output, Contains.Substring("Pillow"));
            Assert.That(output, Contains.Substring("termcolor"));
            // we should not have any more packages
            var newLineRegex = new Regex(@"\r\n|\n|\r");
            var lines = newLineRegex.Split(output);
            Assert.That(lines.Length, Is.EqualTo(9)); // 8 package lines + 1 empty line

            argsStr = new PyString(" -m pip install pip -U");
            args = shlex.split(argsStr);

            dynamic envOverride = new PyDict();
            envOverride["PYTHONPATH"] = new PyString(pythonPath);
            dynamic proc2 = spawn(pythonVenvInterpreter, args,
                    env_override: envOverride,
                    wantLogging: false);
            yield return WaitForProcessEnd(proc2, 20);

            // Call UpdatePackages with a requirements.txt file containing:
            // numpy==1.18.2
            // vg==1.7.0
            // Brotli==1.0.7
            var testRequirementFile = Path.Combine(TestsPath, "test_requirements_3.txt");
            PipPackages.UpdatePackages(testRequirementFile, pythonVenvInterpreter);

            var output2 = CallPipFreeze(pythonVenvInterpreter, pythonPath);
            Assert.That(output2, Contains.Substring("numpy==1.18.2"));
            Assert.That(output2, Contains.Substring("vg==1.7.0"));
            Assert.That(output2, Contains.Substring("Brotli==1.0.7"));
            var lines2 = newLineRegex.Split(output2);
            Assert.That(lines2.Length, Is.EqualTo(4));
        }
        }
    }

    [Test]
    public void TestIsInterestingWarning()
    {
        string unwantedWarningMsg1 = "WARNING: The scripts coverage-3.7.exe, coverage.exe and coverage3.exe are installed in 'D:\\UnityProjects\\Python 3 - Copy\\Library\\PythonInstall\\Scripts' which is not on PATH.";
        string unwantedWarningMsg2 = "Consider adding this directory to PATH or, if you prefer to suppress this warning, use --no-warn-script-location.";
        string unwantedWarningMsg3 = "WARNING: You are using pip version 20.0.2; however, version 20.1 is available.";
        string unwantedWarningMsg4 = "You should consider upgrading via the 'D:\\UnityProjects\\Python 3 - Copy\\Library\\PythonInstall\\python.exe -m pip install --upgrade pip' command.";

        string wantedWarningMsg = "Command \"python setup.py egg_info\" failed with error code 1 in C:\\Users\\foo\\AppData\\Local\\Temp\\pip-install-ws21otxr\\psycopg2\\";

        Assert.That(PipPackages.IsInterestingWarning(unwantedWarningMsg1), Is.False);
        Assert.That(PipPackages.IsInterestingWarning(unwantedWarningMsg2), Is.False);
        Assert.That(PipPackages.IsInterestingWarning(unwantedWarningMsg3), Is.False);
        Assert.That(PipPackages.IsInterestingWarning(unwantedWarningMsg4), Is.False);

        Assert.That(PipPackages.IsInterestingWarning(wantedWarningMsg), Is.True);

        // try with null or empty message
        Assert.That(PipPackages.IsInterestingWarning(null), Is.False);
        Assert.That(PipPackages.IsInterestingWarning(""), Is.False);
    }
    }

    internal class LoadPipRequirementsTest
    {
        private bool m_pipRequirementsExists = false;
        private string m_origPipRequirementsContents;

        [SetUp]
        public void Init()
        {
            if (File.Exists(PythonSettings.kPipRequirementsFile))
            {
                m_pipRequirementsExists = true;
                m_origPipRequirementsContents = File.ReadAllText(PythonSettings.kPipRequirementsFile);
            }
        }

        [TearDown]
        public void Term()
        {
            if (m_pipRequirementsExists)
            {
                // make sure original requirements are reset
                File.WriteAllText(PythonSettings.kPipRequirementsFile, m_origPipRequirementsContents);
                SessionState.SetBool(LoadPipRequirements.k_onStartup, true);
                LoadPipRequirements.LoadRequirements();
            }
            else
            {
                // delete file as it didn't exist before the tests ran.
                File.Delete(PythonSettings.kPipRequirementsFile);
            }
        }

        [Test]
        public void TestNoPipRequirements()
        {
            // check that everything works without a requirements.txt file.
            if (m_pipRequirementsExists) {
                File.Delete(PythonSettings.kPipRequirementsFile);
            }
            Assert.That(PythonSettings.kPipRequirementsFile, Does.Not.Exist);

            var output = PipPackagesTest.CallPipFreeze();

            // reset on startup variable to simulate Unity starting up
            SessionState.SetBool(LoadPipRequirements.k_onStartup, true);
            LoadPipRequirements.LoadRequirements();

            // output of pip freeze should be the same
            Assert.That(PipPackagesTest.CallPipFreeze(), Is.EqualTo(output));
        }

        [Test]
        public void TestValidPipRequirements()
        {
            // add a package to the pip requirements
            var testRequirementFile = Path.Combine(PipPackagesTest.TestsPath, "test_requirements_2.txt");
            Assert.That(testRequirementFile, Does.Exist);
            string fileContents = File.ReadAllText(testRequirementFile);

            // To avoid uninstalling any packages, if a requirements file already exists,
            // base the test requirements on this file.
            if (m_pipRequirementsExists)
            {
                // TODO: what to test if toml is already installed at the expected version?
                Assert.That(m_origPipRequirementsContents, Does.Not.Contain("toml"), "Is toml already in your requirements.txt file?");
                fileContents += "\n" + m_origPipRequirementsContents;
            }
            Assert.That(fileContents, Is.Not.Null.Or.Empty);

            File.WriteAllText(PythonSettings.kPipRequirementsFile, fileContents);

            // reset on startup variable to simulate Unity starting up
            SessionState.SetBool(LoadPipRequirements.k_onStartup, true);

            LoadPipRequirements.LoadRequirements();

            var packageUpdateRegex = new Regex("The Project's following Python packages have been updated:.*");
            LogAssert.Expect(LogType.Log, packageUpdateRegex);

            // Check that toml was updated to 0.9.0
            var output = PipPackagesTest.CallPipFreeze();
            Assert.That(output, Does.Contain("toml==0.9.0\n"));

            // Test that the requirements are not reloaded again if they change (only loaded at startup)
            var newFileContents = m_pipRequirementsExists ? m_origPipRequirementsContents : "";
            File.WriteAllText(PythonSettings.kPipRequirementsFile, newFileContents);

            LoadPipRequirements.LoadRequirements();

            // toml should still be there as the requirements were not updated
            output = PipPackagesTest.CallPipFreeze();
            Assert.That(output, Does.Contain("toml==0.9.0\n"));
        }
    }
}
