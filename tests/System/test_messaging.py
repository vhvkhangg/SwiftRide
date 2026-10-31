import unittest
from unittest.mock import MagicMock, patch
import messaging


class HttpTests(unittest.TestCase):
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


if __name__ == "__main__":
    unittest.main()
