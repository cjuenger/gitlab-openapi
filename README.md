# gitlab-openapi

## Run Swagger UI locally to evaluate and try out changes on the OpenAPI specification

```
docker run --name my-swagger-ui -p 80:8080 -e URL=api/openapi.yaml -v ${PWD}://usr/share/nginx/html/api swaggerapi/swagger-ui:v3.51.1
```

## Generate a C# client from the OpenAPI specification

```
docker run --rm -v ${PWD}:/local openapitools/openapi-generator-cli generate -g csharp-netcore --additional-properties=targetFramework=net5.0 --additional-properties=packageName=IO.Juenger.GitLabClient -i /local/openapi.yaml -o /local/generated/client
```

### Adjust the generated client library

The README of the generated client recommends to update several dependencies. So let's do this. 

Open the solution and update the dependencies of the project `Io.Juenger.GitLabClient`. Look to it that you only update minor and/or patch version. Other versions could cause problems.

### Pack the generated client library

Go to the project folder `generated/client/src/Io.Juenger.GitLabClient` and pack the library:

`dotnet pack -o package`

### Publish the client library

Follow the instructions at https://docs.microsoft.com/en-us/nuget/nuget-org/publish-a-package.