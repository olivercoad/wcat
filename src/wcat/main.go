package main

import (
	"bytes"
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

func resizeImage(f io.Reader, maxWidth, maxHeight int) (*io.PipeReader, error) {
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
func PreviewFile(client *http.Client, wcatserver string, filename string, input io.ReadSeeker, maxwidth, maxheight int) {

	contentType, err := mimetype.DetectReader(input)
	extension := strings.ToLower(filepath.Ext(filename))
	if err != nil {
		fmt.Println(aurora.Red(err))
		return
	}
	_, err = input.Seek(0, 0)
	if err != nil {
		fmt.Println(aurora.Red(err))
		return
	}
	var requestBody io.Reader = input
	if contentType.Is("image/jpeg") {
		resizedBodyReader, err := resizeImage(input, maxwidth, maxheight)
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
		Name:      "wcat",
		Usage:     "Send FILE(s) to be previewed in a browser.",
		UsageText: "wcat [OPTIONS] ...  [FILE] ...\n\nWith no FILE(s), read from standard input.",
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
			client := &http.Client{}
			wcatserver := c.String("server")
			maxwidth := c.Int("maxwidth")
			maxheight := c.Int("maxheight")
			if c.NArg() >= 1 {
				for i := 0; i < c.NArg(); i++ {
					filename := c.Args().Get(i)
					f, err := os.Open(filename)

					if err != nil {
						fmt.Println(aurora.Red(err))
					} else {
						defer f.Close()
						fmt.Print("Preview file ", filename, " ... ")
						PreviewFile(client, wcatserver, filename, f, maxwidth, maxheight)
					}
				}
			} else {
				fmt.Print("Previewing from standard input... ")

				// must read into memory for seeking to 0 after detecting mimetype
				input, err := ioutil.ReadAll(os.Stdin)
				if err != nil {
					fmt.Println(aurora.Red(err))
				} else {
					reader := bytes.NewReader(input)
					filename := "stdin"
					PreviewFile(client, wcatserver, filename, reader, maxwidth, maxheight)
				}

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
