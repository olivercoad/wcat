package main

import (
	"fmt"
	"image/jpeg"
	"io"
	"io/ioutil"
	"log"
	"math"
	"net/http"
	"os"
	"path/filepath"
	"strings"

	"github.com/gabriel-vasile/mimetype"
	"github.com/nfnt/resize"

	"github.com/logrusorgru/aurora"
	"github.com/urfave/cli/v2"
)

func resizeImage(f *os.File, maxWidth, maxHeight int) (*io.PipeReader, error) {
	img, err := jpeg.Decode(f)
	if err != nil {
		return nil, err
	}
	if maxWidth <= 0 {
		maxWidth = math.MaxInt32
	}
	if maxHeight <= 0 {
		maxHeight = math.MaxInt32
	}
	mW, mH := uint(maxWidth), uint(maxHeight)
	resized := resize.Thumbnail(mW, mH, img, resize.Lanczos3)
	outr, outw := io.Pipe()
	go func() {
		defer outw.Close()
		jpeg.Encode(outw, resized, nil)
	}()
	return outr, nil
}

// PreviewFile posts the file to the wcat server with filename and Content-Type headers.
func PreviewFile(client *http.Client, wcatserver string, filename string, maxwidth, maxheight int) {
	fmt.Print("Preview file ", filename, " ... ")

	f, err := os.Open(filename)
	if err != nil {
		fmt.Println(aurora.Red(err))
		return
	}
	defer f.Close()

	contentType, err := mimetype.DetectReader(f)
	extension := strings.ToLower(filepath.Ext(filename))
	if err != nil {
		fmt.Println(aurora.Red(err))
		return
	}
	f.Seek(0, 0)
	var requestBody io.Reader = f
	if contentType.Is("image/jpeg") {
		resizedBodyReader, err := resizeImage(f, maxwidth, maxheight)
		if err != nil {
			fmt.Println(aurora.Red(err))
			return
		}
		defer resizedBodyReader.Close()
		requestBody = resizedBodyReader
	}

	req, err := http.NewRequest("POST", wcatserver+"/api/showthis", requestBody)
	if err != nil {
		fmt.Println(aurora.Red("Error making request"), err)
		return
	}
	if contentType.Is("text/plain") && extension == ".md" {
		req.Header.Set("Content-Type", "text/markdown")
	} else {
		req.Header.Set("Content-Type", contentType.String())
	}

	req.Header.Set("filename", filepath.Base(filename))
	resp, err := client.Do(req)
	if err != nil {
		fmt.Println(aurora.Red("Error doing post request"), err)
		return
	}
	defer resp.Body.Close()
	body, err := ioutil.ReadAll(resp.Body)
	if err != nil {
		fmt.Println(aurora.Red("Error reading response body"), err)
		return
	}
	if resp.StatusCode >= 200 && resp.StatusCode < 300 {
		fmt.Println(aurora.Cyan(resp.Status))
	} else {
		fmt.Println(aurora.Red(resp.Status))
	}
	fmt.Printf("%s\n", body)
}

func main() {
	app := &cli.App{
		Name:  "wcat",
		Usage: "Send files to be previewed in a browser",
		Flags: []cli.Flag{
			&cli.StringFlag{
				Name:    "server",
				Value:   "http://localhost:8085",
				Usage:   "Post previews to `SERVER`",
				EnvVars: []string{"WCATSERVER"},
			},
			&cli.IntFlag{
				Name:    "maxwidth",
				Aliases: []string{"mw"},
				Value:   1024,
				Usage:   "the max width for images. 0 for no max width",
			},
			&cli.IntFlag{
				Name:    "maxheight",
				Aliases: []string{"mh"},
				Value:   800,
				Usage:   "the max height for images. 0 for no max height",
			},
		},
		Action: func(c *cli.Context) error {
			if c.NArg() >= 1 {
				client := &http.Client{}
				wcatserver := c.String("server")
				maxwidth := c.Int("maxwidth")
				maxheight := c.Int("maxheight")
				for i := 0; i < c.NArg(); i++ {
					filename := c.Args().Get(i)
					PreviewFile(client, wcatserver, filename, maxwidth, maxheight)
				}
			} else {
				fmt.Println("No files specified")
			}
			return nil
		},
		Version: version,
	}

	err := app.Run(os.Args)
	if err != nil {
		log.Fatal(err)
	}
}
