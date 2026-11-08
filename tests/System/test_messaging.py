import unittest
import subprocess
import tempfile
from pathlib import Path
from unittest.mock import MagicMock, patch
import messaging


class HttpTests(unittest.TestCase):
    def test_command_failure_keeps_redacted_combined_output(self):
        with tempfile.TemporaryDirectory() as directory:
            stack = object.__new__(messaging.Stack)
            stack.env = {}
            stack.secrets = ["test-secret"]
            stack.output = Path(directory)
            with patch("subprocess.run", return_value=subprocess.CompletedProcess(
                    ["docker"], 1, stdout="pull progress\ncontainer failed: test-secret")) as run:
                with self.assertRaisesRegex(RuntimeError, r"container failed: \[redacted\]"):
                    stack.run(["docker"])
            self.assertEqual(subprocess.STDOUT, run.call_args.kwargs["stderr"])
            log = (stack.output / "command-failure.log").read_text(encoding="utf-8")
            self.assertIn("container failed: [redacted]", log)
            self.assertNotIn("test-secret", log)

    def test_health_plain_text_and_json_response(self):
        response = MagicMock()
        response.status = 200
        response.headers = {"Content-Type": "text/plain"}
        response.read.return_value = b"Healthy"
        with patch("urllib.request.urlopen") as open_url:
            open_url.return_value.__enter__.return_value = response
            self.assertEqual("Healthy", messaging.http("GET", "http://localhost/health/ready"))
            response.headers = {"Content-Type": "application/json"}
            response.read.return_value = b'{"ready":true}'
            self.assertEqual({"ready": True}, messaging.http("GET", "http://localhost/health"))

    def test_wait_timeout_fails_instead_of_reporting_pass(self):
        with self.assertRaises(TimeoutError):
            messaging.wait(lambda: False, seconds=0)

    def test_rabbit_create_delete_statuses_are_explicit(self):
        response = MagicMock()
        response.read.return_value = b""
        with patch("urllib.request.urlopen") as open_url:
            open_url.return_value.__enter__.return_value = response
            for method, status in (("PUT", 201), ("POST", 201), ("DELETE", 204)):
                with self.subTest(method=method):
                    response.status = status
                    self.assertIsNone(messaging.http(method, "http://localhost/api/queues", expected=status))
                    with self.assertRaises(AssertionError):
                        messaging.http(method, "http://localhost/api/queues")


if __name__ == "__main__":
    unittest.main()
