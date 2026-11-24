"""Shared setup: the server's Hypothesis profiles (run.py puts the server repo on sys.path) and paths."""
import os

from hypothesis import settings
from hypothesis.database import DirectoryBasedExampleDatabase

from test.strategies.base import DEEP, PROFILE, deep_only, examples  # noqa: F401  (the server's profiles)

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
CLIENT = os.environ.get("ORI_CLIENT_REPO", REPO)
SERVER = os.environ.get("ORI_SERVER_REPO", os.path.join(os.path.dirname(REPO), "ori_rando_server"))

if PROFILE == "dev":
    # dev keeps failing examples, here rather than in the server's .hypothesis
    settings.register_profile("client-dev", parent=settings.get_profile("dev"),
                              database=DirectoryBasedExampleDatabase(os.path.join(HERE, ".hypothesis")))
    settings.load_profile("client-dev")


def client_file(rel):
    with open(os.path.join(CLIENT, rel), "rb") as f:
        return f.read()
