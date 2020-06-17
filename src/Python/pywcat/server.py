import os
from requests import post
from requests.exceptions import ConnectionError
import urllib3
wcatserver = os.getenv("WCATSERVER", "http://localhost:8085")

def post_file(value_or_io, content_type, name):
    headers = {
        'Content-Type': content_type,
        'filename': name
    }
    try:
        r = post(wcatserver + '/api/showthis', value_or_io, headers = headers)
        print(r.text)
    except ConnectionError:
        print("Failed to make request to wcat server at " + wcatserver)