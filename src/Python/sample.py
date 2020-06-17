# Use matplotlib with wcat - displayed with SVG format for best quality

import matplotlib.pyplot as plt
import pywcat.matplotlib as wplt

plt.plot([1,3,2,4])
plt.ylabel('some more number stuff')
plt.title("Matplotlib with wcat")

wplt.show("Example Matplotlib plot")




# Embed bokeh plot in wcat output

from bokeh.plotting import figure, output_file, show
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




# Show PIL image. Defaults to PNG format

from PIL import Image, ImageDraw, ImageFont
from pywcat.PIL import show

img = Image.new('RGB', (200, 60), color = (73, 109, 137))

d = ImageDraw.Draw(img)
d.text((10,10), "Pillow PIL with wcat as PNG", fill=(255, 255, 0))

show(img, "PIL PNG")

# Show PIL image with JPEG format - compressed as jpeg makes faster load time

img2 = Image.new('RGB', (200, 60), color = (73, 109, 137))

d = ImageDraw.Draw(img2)
d.text((10,10), "Pillow PIL with wcat as JPEG", fill=(255, 255, 0))

bigger = img2.resize((400, 120)) # Use PIL to resize - for large images, consider downsizing for faster load

show(bigger, "PIL JPEG", format="JPEG")



show(Image.open('hello.jpg'))