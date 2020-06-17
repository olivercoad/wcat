from pywcat.server import post_file
from bokeh.resources import CDN
from bokeh.embed import file_html

def show(plot, name = 'plt'):
    html = file_html(plot, CDN, name)
    post_file(html, 'text/html', name)