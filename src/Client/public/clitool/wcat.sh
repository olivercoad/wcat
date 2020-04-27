#!/usr/bin/env bash

for f in "$@"
do
    mimetype=$(file -b --mime-type "$f")
    curl --location --request POST "${WCATSERVER:-http://localhost:8085}/api/showthis" \
        --header "filename: $f" \
        --header "Content-Type: $mimetype" \
        --data-binary "@$f"
done