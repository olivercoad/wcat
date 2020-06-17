# pywcat

A set of simple python helpers for sending images and plots to wcat for preview.

This is a useful alternative to jupyter for previews on a remote machine.

## Setup

First, make sure you are running a wcat server https://github.com/olivercoad/wcat/#setup and can access it in your browser.

Once you have a wcat server running (on a local or remote machine), install pywcat.

```bash
pip install pywcat
```

## Usage

See `sample.py` for usage example.

### matplotlib
Plots are displayed with SVG format for the best quality.
```python
import matplotlib.pyplot as plt
import pywcat.matplotlib as wplt

plt.plot([1,3,2,4])
plt.ylabel('some more number stuff')
plt.title("Matplotlib with wcat")

wplt.show("Example Matplotlib plot")
```

The `**kwargs` on `wplt.show` are passed down to [`matplotlib.pyplot.savefig`](https://matplotlib.org/3.1.1/api/_as_gen/matplotlib.pyplot.savefig.html) to allow for more control over output.

### bokeh

```python
from bokeh.plotting import figure, output_file
from pywcat.bokeh import show

# prepare some data
x = [1, 2, 3, 4, 5]
y = [6, 7, 2, 4, 5]

# create a new plot with a title and axis labels
p = figure(title="Bokeh with wcat", x_axis_label='x', y_axis_label='y')

# add a line renderer with legend and line thickness
p.line(x, y, legend_label="Temp.", line_width=2)

# show the results
show(p, "Example Bokeh plot")
```

### Pillow

Defaults to PNG format.

```python
from PIL import Image, ImageDraw, ImageFont
from pywcat.PIL import show

img = Image.new('RGB', (200, 60), color = (73, 109, 137))

d = ImageDraw.Draw(img)
d.text((10,10), "Pillow PIL with wcat as PNG", fill=(255, 255, 0))

show(img, "PIL PNG")
```

Use JPEG format for faster load time

```python
show(img, "PIL JPEG", format="JPEG")
```

## Usage on a remote machine
It is recommended to send previews on the same machine that the server is running.

If you follow the instructions for [using wcat on a remote machine](https://github.com/olivercoad/wcat/#usage-on-a-remote-machine) and the wcat server accessible on `http://localhost:8085` then it will work by default.

Otherwise, this can be changed for pywcat with the `WCATSERVER` environment variable:

```bash
export WCATSERVER=http://localhost:8085
python sample.py
```

or by setting the value in python:

```python
import pywcat.server
pywcat.server.wcatserver = "http://localhost:8085"
```