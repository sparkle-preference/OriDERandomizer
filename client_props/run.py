"""Runs the client property suite with the server's venv python, from the client repo root:
    ..\\ori_rando_server\\.venv\\Scripts\\python.exe client_props\\run.py [name-filter] [-v]
HYPOTHESIS_PROFILE picks dev (default), ci or deep; ORI_HARNESS=1 adds the in-game slice."""
import os
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
SERVER = os.environ.get("ORI_SERVER_REPO", os.path.join(os.path.dirname(REPO), "ori_rando_server"))


def setup():
    # server modules read their data files relative to the repo root
    os.chdir(SERVER)
    sys.path[:0] = [REPO, SERVER]


def main(argv):
    verbose = "-v" in argv
    names = [a for a in argv if a != "-v"]
    setup()
    pattern = "*%s*_props.py" % names[0] if names else "*_props.py"
    suite = unittest.defaultTestLoader.discover(HERE, pattern=pattern, top_level_dir=REPO)
    result = unittest.TextTestRunner(verbosity=2 if verbose else 1).run(suite)
    return 0 if result.wasSuccessful() else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
