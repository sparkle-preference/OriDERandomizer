"""Runs a slice's expectedFailure tests bare and prints where each one fails: python bare_markers.py <module>."""
import os
import sys
import traceback
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import run  # noqa: E402

run.setup()


def main(name):
    mod = __import__("client_props." + name, fromlist=["x"])
    if hasattr(mod, "setUpModule"):
        mod.setUpModule()
    for cls in [v for v in vars(mod).values() if isinstance(v, type) and issubclass(v, unittest.TestCase)]:
        markers = [n for n, f in vars(cls).items() if getattr(f, "__unittest_expecting_failure__", False)]
        if not markers:
            continue
        cls.setUpClass()
        try:
            for n in markers:
                case = cls(n)
                try:
                    case.setUp()
                    getattr(case, n)()
                    print("PASSES (no longer a bug?):", n)
                except Exception as e:
                    frame = traceback.extract_tb(e.__traceback__)[-1]
                    print("%s\n    %s: %s\n    at %s:%d %s" % (n, type(e).__name__, str(e)[:240], os.path.basename(frame.filename),
                                                            frame.lineno, frame.line))
                finally:
                    case.doCleanups()
        finally:
            cls.tearDownClass()


if __name__ == "__main__":
    main(sys.argv[1])
