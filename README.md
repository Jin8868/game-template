# Game Template

Unity 2022.3 game project template backed by the independently versioned Alloy Framework package.

## Setup

Clone this repository, then run `setup.bat` on Windows or `./setup.sh` on macOS/Linux. The setup script clones Alloy Framework into the ignored `AlloyFramework/` directory and applies repository-local Git settings.

Open `UnityProj` as the Unity project. Its package manifest references Alloy Framework through `file:../../AlloyFramework`, so framework edits are compiled immediately.

The game template and framework remain separate repositories. Commit game changes from the repository root and framework changes from `AlloyFramework/`.
