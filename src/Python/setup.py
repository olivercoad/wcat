import codecs
import pathlib
from setuptools import setup

# The directory containing this file
HERE = pathlib.Path(__file__).parent

# The text of the README file
README = (HERE / "README.md").read_text()

def get_version(rel_path):
    for line in (HERE / rel_path).read_text('utf-8-sig').splitlines():
        if line.startswith('__version__'):
            delim = '"' if '"' in line else "'"
            return line.split(delim)[1]
    else:
        raise RuntimeError("Unable to find version string.")

# This call to setup() does all the work
setup(
    name="pywcat",
    version=get_version("pywcat/__init__.py"),
    description="Read the latest Real Python tutorials",
    long_description=README,
    long_description_content_type="text/markdown",
    url="https://github.com/olivercoad/wcat",
    author="Oliver Coad",
    author_email="oliver.coad@gmail.com",
    license="MIT",
    classifiers=[
        "License :: OSI Approved :: MIT License",
        "Programming Language :: Python :: 3",
        "Programming Language :: Python :: 3.7",
    ],
    packages=["pywcat"],
    include_package_data=True,
    install_requires=["requests"],
)