import matplotlib.pyplot as plt
import io
from pywcat.server import post_file

def show(name = 'plt', **kwargs):
    with io.BytesIO() as buf:
        kwargs['format'] = 'svg' # override format to svg
        plt.savefig(buf, **kwargs) # allow caller to specify other args for savefig
        buf.seek(0)
        post_file(buf, 'image/svg+xml', name)