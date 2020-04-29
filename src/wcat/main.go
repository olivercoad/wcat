package main

import (
	"fmt"
	"io/ioutil"
	"log"
	"net/http"
	"os"
	"path/filepath"
	"strings"

	"github.com/gabriel-vasile/mimetype"

	"github.com/logrusorgru/aurora"
	"github.com/urfave/cli/v2"
)

// PreviewFile posts the file to the wcat server with filename and Content-Type headers.
func PreviewFile(client *http.Client, wcatserver string, filename string) {
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
	req, err := http.NewRequest("POST", wcatserver+"/api/showthis", f)
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
		},
		Action: func(c *cli.Context) error {
			if c.NArg() >= 1 {
				client := &http.Client{}
				wcatserver := c.String("server")
				for i := 0; i < c.NArg(); i++ {
					filename := c.Args().Get(i)
					PreviewFile(client, wcatserver, filename)
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
