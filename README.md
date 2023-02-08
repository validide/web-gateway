# web-gateway
Web Gateway

A simple reverse proxy with HTTPS support built on top of:
* [Microsoft YARP](https://github.com/microsoft/reverse-proxy)
* [LettuceEncrypt](https://github.com/natemcmaster/LettuceEncrypt)


## Run the gateway

Create an `environment-configuration.json` configuration with your setting. See the sample below:

``` json
{
  "// Urls": "Add the 443 url. If you want HTTPS.",
  "Urls": "http://*:80;https://*:443",
  "LettuceEncrypt": {
    "AcceptTermsOfService": true,
    "DomainNames": [ "your.domain.com" ],
    "EmailAddress": "you@some-mail.com",
    "PersistPassword": "Password to encrypt the HTTPS certificate information",
    "// PersistPath": "Path where the encrypted the HTTPS certificate information is stored. Defaults to ../app_data/",
    "PersistPath": "../app_data/"
  },
  "// ReverseProxy": "Check the Microsoft YARP configuration and add your proxy configurations",
  "ReverseProxy": {
    "Routes": {
      "httpbin_route": {
        "ClusterId": "httpbin_cluster",
        "Match": {
          "Path": "/.http-bin-debug/{**remainder}"
        },
        "Transforms": [
          {
            "PathRemovePrefix": "/.http-bin-debug"
          }
        ]
      },
      "host_aaaaa_or_bbbbb_route": {
        "ClusterId": "aaaaa_or_bbbbb_cluster",
        "Match": {
          "Path": "{**catch-all}",
          "Hosts" : [ "www.aaaaa.com", "www.bbbbb.com"],
        }
      }
    },
    "Clusters": {
      "httpbin_cluster": {
        "Destinations": {
          "httpbin.org": {
            "Address": "https://httpbin.org/"
          }
        }
      },
      "aaaaa_or_bbbbb_cluster": {
        "Destinations": {
          "aaaaa_or_bbbbb_host": {
            "Address": "http://aaaaa_or_bbbbb_host:8080/"
          }
        }
      }
    }
  }
}
```

Start a new container.

``` sh
docker run -p 9080:80 -p 9443:443 \
  -v $(pwd)/environment-configuration.json:/app/environment-configuration.json \
  -v /some/path-to-keep/secrets:/app_data \
  web-gateway:2023.02
```

Test the paths:

``` sh
curl -X GET "https://your.domain.com/.info"
curl -X GET "https://httpbin.org/get" -H "accept: application/json"
curl -X GET "https://your.domain.com/.http-bin-debug/get" -H "accept: application/json"

```

## Build

``` sh
docker build -t validide/web-gateway:2023.02.1 -f build.dockerfile .
```
