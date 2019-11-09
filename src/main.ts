import * as core from "@actions/core";
import * as exec from "@actions/exec";
import * as io from "@actions/io";
import * as path from "path";

async function run()
{
  try
  {
    const IS_WINDOWS = process.platform === "win32";
    if (IS_WINDOWS)
    {
      let escapedSolution = `${path.join(__dirname, '..', 'externals', 'SetupModLoader.csproj').replace(/'/g, "''")}`;
      const dotnetPath = await io.which("dotnet", true);
      await exec.exec(`"${dotnetPath}"`, ["run", "--project", escapedSolution]);
    }
  }
  catch (error)
  {
    core.setFailed(error.message);
  }
}

run();
