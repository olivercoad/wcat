import io
from pywcat.server import post_file

defaultFormat = "PNG"

def show(img, name = 'img', format = None):
    if format is None:
        format = defaultFormat
    if format == "JPEG":
        content_type = "image/jpeg"
    elif format == "PNG":
        content_type = "image/png"
    else:
        raise Exception("format must be JPEG or PNG")

    with io.BytesIO() as buf:
        img.save(buf, format=format) # allow caller to specify other args for savefig
        buf.seek(0)
        post_file(buf, content_type, name)