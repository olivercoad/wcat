package main

import (
	"bytes"
	"errors"
	"fmt"
	"image"
	"image/jpeg"
	"image/png"
	"io"
	"io/ioutil"
	"log"
	"math"
	"net/http"
	"net/url"
	"os"
	"path/filepath"
	"regexp"
	"strings"

	"github.com/gabriel-vasile/mimetype"
	"github.com/nfnt/resize"
	"golang.org/x/image/webp"

	"github.com/logrusorgru/aurora"
	"github.com/urfave/cli/v2"
)

func isResizeImage(contentType *mimetype.MIME) bool {
	return contentType.Is("image/jpeg") || contentType.Is("image/png")
}

func readImage(f io.Reader, contentType *mimetype.MIME) (image.Image, error) {
	if contentType.Is("image/jpeg") {
		return jpeg.Decode(f)
	} else if contentType.Is("image/png") {
		return png.Decode(f)
	} else if contentType.Is("image/webp") {
		return webp.Decode(f)
	} else {
		return nil, errors.New(fmt.Sprint("Resizing not supported for ", contentType.String()))
	}
}

func resizeImage(img image.Image, maxWidth, maxHeight int) *io.PipeReader {
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
	return outr
}

// postFile posts the file to the wcat server with filename and Content-Type headers.
func postFile(client *http.Client, wcatserver string, filename string, contentTypeHeader string, requestBody io.Reader, justFile bool) {
	req, err := http.NewRequest("POST", wcatserver+"/api/showthis", requestBody)
	if err != nil {
		fmt.Println(aurora.Red("Error making request"), err)
		return
	}
	if justFile {
		req.Header.Set("Content-Type", "application/octet-stream")
		req.Header.Set("justfile", "true")
	} else {
		req.Header.Set("Content-Type", contentTypeHeader)
	}

	req.Header.Set("filename", filepath.Base(filename))
	resp, err := client.Do(req)
	if err != nil {
		fmt.Println(aurora.Red("Error doing POST request"), err)
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
		fmt.Printf("%s\n", body)
	}
}

// PreviewFile optionally detects the content type and resizes images before calling postFile
func PreviewFile(client *http.Client, wcatserver string, filename string, input io.ReadSeeker, maxwidth, maxheight int, justFile bool) {
	if justFile {
		var requestBody io.Reader = input
		var contentTypeHeader = "application/octet-stream"
		postFile(client, wcatserver, filename, contentTypeHeader, requestBody, justFile)
	} else {
		contentType, err := mimetype.DetectReader(input)
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
		if isResizeImage(contentType) {
			img, err := readImage(input, contentType)
			if err != nil {
				fmt.Println(aurora.Red(err))
				return
			}
			resizedBodyReader := resizeImage(img, maxwidth, maxheight)
			defer resizedBodyReader.Close()
			requestBody = resizedBodyReader
		}

		extension := strings.ToLower(filepath.Ext(filename))

		contentTypeHeader := contentType.String()

		if contentType.Is("text/plain") && extension == ".md" {
			contentTypeHeader = "text/markdown"
		}
		postFile(client, wcatserver, filename, contentTypeHeader, requestBody, justFile)
	}
}

func decodeFilename(filenameEnc string) (string, error) {
	filename, err := url.QueryUnescape(filenameEnc)
	if err != nil {
		return "", err
	}
	//clean the filename to hopefully be a safe name and be a direct child of provided directory (even if malicious filename header)
	basename := filepath.Base(filepath.Clean(filename))
	re := regexp.MustCompile(`[\\\/:"*?<>|\n\r]+`)
	sanitizedBasename := re.ReplaceAllLiteralString(basename, "")
	return strings.TrimSpace(sanitizedBasename), nil
}

func downloadLatestFile(client *http.Client, wcatserver string, printStatus bool) (io.ReadCloser, string, error) {
	if printStatus {
		fmt.Print("Downloading latest file ... ")
	}
	req, err := http.NewRequest("GET", wcatserver+"/api/downloadfile", nil)
	if err != nil {
		if printStatus {
			fmt.Println(aurora.Red("Error making request"))
		}
		return nil, "", err
	}
	resp, err := client.Do(req)
	if err != nil {
		if printStatus {
			fmt.Println(aurora.Red("Error doing GET request"))
		}
		return nil, "", err
	}

	if resp.StatusCode >= 200 && resp.StatusCode < 300 {
		if printStatus {
			fmt.Print(aurora.Cyan(resp.Status))
		}
		filenameEnc := resp.Header.Get("filename")
		filename, err := decodeFilename(filenameEnc)
		if err != nil {
			if printStatus {
				fmt.Println(aurora.Red(", Error decoding filename"))
			}
			filename = ""
		} else if printStatus {
			fmt.Println(aurora.Cyan(fmt.Sprint(", ", filename)))
		}
		return resp.Body, filename, nil
	}
	defer resp.Body.Close()
	body, err := ioutil.ReadAll(resp.Body)
	if err != nil {
		if printStatus {
			fmt.Println(aurora.Red(fmt.Sprint("Cannot read body ", resp.Status)))
		}
		return nil, "", err
	}

	if printStatus {
		fmt.Println(aurora.Red(resp.Status))
		fmt.Printf("%s\n\n", body)
	}

	if resp.StatusCode == 409 && strings.Contains(fmt.Sprintf("%s", body), "There is nothing to preview") {
		return nil, "", errors.New("No latest file")
	}

	return nil, "", errors.New(fmt.Sprint("Response status code ", resp.StatusCode))
}

func downloadFileDirectlyToFile(client *http.Client, wcatserver string, path string, overwrite bool) error {
	var file *os.File
	if overwrite {
		var err error
		file, err = os.Create(path) //truncate if exists
		if err != nil {
			fmt.Println(aurora.Red("Cannot create file"))
			return err
		}
		defer file.Close()
	} else {
		var err error
		file, err = os.OpenFile(path, os.O_RDWR|os.O_CREATE|os.O_EXCL, 0600)
		if err != nil {
			if os.IsExist(err) {
				fmt.Println(aurora.Red("File already exists"))
			} else {
				fmt.Println(aurora.Red("Cannot create file"))
			}
			return err
		}
		defer file.Close()
	}

	body, _, err := downloadLatestFile(client, wcatserver, true)
	if err != nil {
		return err
	}
	defer body.Close()
	fmt.Print("Copying response to output file ... ")
	_, err = io.Copy(file, body)
	if err != nil {
		fmt.Println(aurora.Red(err))
		body.Close()
		return err
	}
	if err := body.Close(); err != nil {
		fmt.Println(aurora.Red(err))
		return err
	}

	if err := file.Close(); err != nil {
		fmt.Println(aurora.Red("Failed to close file"))
		return err
	}
	fmt.Println(aurora.Cyan(file.Name()))
	return nil
}

func downloadFileToDirectory(client *http.Client, wcatserver string, path string, overwrite bool) error {
	directory, err := os.Open(path)
	if err != nil {
		fmt.Println(aurora.Red("Cannot open directory"))
		return err
	}
	fi, err := directory.Stat()
	if err != nil {
		fmt.Println(aurora.Red("Could not get fileinfo"))
		return err
	}
	if !fi.IsDir() {
		fmt.Println(aurora.Red("Not a directory"), directory.Name())
		return errors.New(fmt.Sprint("Location is not a directory: ", directory.Name()))
	}

	body, filename, err := downloadLatestFile(client, wcatserver, true)
	if err != nil {
		return err
	}
	defer body.Close()

	if filename == "" {
		fmt.Println("Latest file has no filename. Please specify output filename")
		return errors.New("Response filename header is empty")
	}

	fullFilename := filepath.Join(directory.Name(), filename)

	fmt.Print("Copying response to output file ")

	var file *os.File
	if overwrite {
		var err error
		file, err = os.Create(fullFilename) //truncate if exists
		if err != nil {
			fmt.Println(aurora.Red("Cannot create file"))
			return err
		}
		defer file.Close()
	} else {
		var err error
		file, err = os.OpenFile(fullFilename, os.O_RDWR|os.O_CREATE|os.O_EXCL, 0600)
		if err != nil {
			if os.IsExist(err) {
				fmt.Println(aurora.Red("File already exists"))
			} else {
				fmt.Println(aurora.Red("Cannot create file"))
			}
			return err
		}
		defer file.Close()
	}

	_, err = io.Copy(file, body)
	if err != nil {
		fmt.Println(aurora.Red(err))
		body.Close()
		return err
	}
	if err := body.Close(); err != nil {
		fmt.Println(aurora.Red(err))
		return err
	}

	if err := file.Close(); err != nil {
		fmt.Println(aurora.Red("Failed to close file"))
		return err
	}
	fmt.Println(aurora.Cyan(file.Name()))
	return nil
}

func downloadFileToStdout(client *http.Client, wcatserver string) error {
	body, _, err := downloadLatestFile(client, wcatserver, false)
	if err != nil {
		return err
	}
	defer body.Close()
	_, err = io.Copy(os.Stdout, body)
	if err != nil {
		return err
	}
	return body.Close()
}

func clearPreviews(client *http.Client, wcatserver string) error {
	fmt.Print("Sending message to clear previews ... ")
	req, err := http.NewRequest("POST", wcatserver+"/api/clearpreviews", nil)
	if err != nil {
		fmt.Println(aurora.Red("Error making request"))
		return err
	}

	resp, err := client.Do(req)
	if err != nil {
		fmt.Println(aurora.Red("Error doing POST request"))
		return err
	}
	defer resp.Body.Close()
	body, err := ioutil.ReadAll(resp.Body)
	if err != nil {
		fmt.Println(aurora.Red("Error reading response body"))
		return err
	}
	if resp.StatusCode >= 200 && resp.StatusCode < 300 {
		fmt.Println(aurora.Cyan("Previews Cleared"))
		return nil
	} else {
		fmt.Println(aurora.Red(resp.Status))
		fmt.Printf("%s\n", body)
		return errors.New("Response code not 2xx")
	}
}

func main() {
	app := &cli.App{
		Name:      "wcat",
		Usage:     "Send FILE(s) to be previewed in a browser.",
		UsageText: "wcat [COMMAND] [OPTIONS] ...  [FILE] ...\n\nWith no FILE(s), read from standard input.",
		Flags: []cli.Flag{
			&cli.StringFlag{
				Name:    "server",
				Aliases: []string{"s"},
				Value:   "http://localhost:8085",
				Usage:   "post previews to `SERVER`",
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
			&cli.BoolFlag{
				Name:  "nomax",
				Value: false,
				Usage: "disable maxwidth and maxheight; shorthand for  --mw 0 --mh 0",
			},
			&cli.BoolFlag{
				Name:    "justfile",
				Aliases: []string{"j"},
				Value:   false,
				Usage:   "don't try to preview/process the file; just upload it as-is for download",
			},
		},
		Action: func(c *cli.Context) error {
			client := &http.Client{}
			wcatserver := c.String("server")
			maxwidth := c.Int("maxwidth")
			maxheight := c.Int("maxheight")
			nomax := c.Bool("nomax")
			justfile := c.Bool("justfile")
			if nomax {
				maxwidth = 0
				maxheight = 0
			}
			if c.NArg() >= 1 {
				for i := 0; i < c.NArg(); i++ {
					filename := c.Args().Get(i)
					f, err := os.Open(filename)

					if err != nil {
						if os.IsNotExist(err) {
							fmt.Println(aurora.Red("File does not exist: "), filename)
						} else {
							fmt.Println(aurora.Red(err))
						}
					} else {
						defer f.Close()
						fmt.Print("Preview file ", filename, " ... ")
						PreviewFile(client, wcatserver, filename, f, maxwidth, maxheight, justfile)
					}
				}
			} else {
				fmt.Print("Previewing from standard input... ")

				// must read into memory for seeking to 0 after detecting mimetype
				input, err := ioutil.ReadAll(os.Stdin)
				if err != nil {
					fmt.Println(aurora.Red(err))
					return cli.Exit("Failed reading stdin", 10)
				}

				reader := bytes.NewReader(input)
				filename := "stdin"
				PreviewFile(client, wcatserver, filename, reader, maxwidth, maxheight, justfile)
			}
			return nil
		},
		Version: version,
		Commands: []*cli.Command{
			{
				Name:    "download",
				Aliases: []string{"d"},
				Usage:   "download the latest file from the server",
				Flags: []cli.Flag{
					&cli.StringFlag{
						Name:    "output-document",
						Aliases: []string{"O"},
						Usage:   "`location` to download to. - for stdout",
						Value:   ".",
					},
					&cli.BoolFlag{
						Name:    "overwrite",
						Aliases: []string{"ow"},
						Usage:   "Overwrite output file if it exists",
					},
				},
				Action: func(c *cli.Context) error {
					// fmt.Print("Downloading file ... ", c.String("output-document"))
					if c.NArg() > 0 {
						return cli.Exit("The download command takes no positional arguments", 11)
					}
					wcatserver := c.String("server")
					outputLoc := c.String("output-document")
					overwrite := c.Bool("overwrite")
					client := &http.Client{}
					if outputLoc == "-" {
						return downloadFileToStdout(client, wcatserver)
					}
					if outputLoc == "." || //check for both incase user is not using os.PathSeparator
						strings.HasSuffix(outputLoc, fmt.Sprint("\\", ".")) || strings.HasSuffix(outputLoc, fmt.Sprint("\\")) ||
						strings.HasSuffix(outputLoc, fmt.Sprint("/", ".")) || strings.HasSuffix(outputLoc, fmt.Sprint("/")) {

						return downloadFileToDirectory(client, wcatserver, outputLoc, overwrite)
					}
					return downloadFileDirectlyToFile(client, wcatserver, outputLoc, overwrite)
				},
			},
			{
				Name:    "clear",
				Aliases: []string{"c"},
				Usage:   "clear previews",
				Flags:   []cli.Flag{},
				Action: func(c *cli.Context) error {
					if c.NArg() > 0 {
						return cli.Exit("The clear command takes no positional arguments", 12)
					}
					wcatserver := c.String("server")
					client := &http.Client{}
					err := clearPreviews(client, wcatserver)
					if err != nil {
						return cli.Exit(err, 13)
					} else {
						return nil
					}
				},
			},
		},
	}

	err := app.Run(os.Args)
	if err != nil {
		log.Fatal(err)
	}
}
