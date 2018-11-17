# ArcGIS Map Server Copy To GeoJson #

Quick and dirty command line exe that copies data from an ArcGIS Dynamic MapServer into local GeoJson files.  

Compiled Windows EXE: `./dist/ArmCop-Latest.7z`.

## Examples ##
---


#### 1. Downloading data from all layers of a map service.  Simply omit the -l parameter. #### 

`armcop -m https://sampleserver6.arcgisonline.com/arcgis/rest/services/Energy/HSEC/MapServer -d d:\tmp\HSEC`

After the the program is finished, the output directory will look like:

    D:\TMP\HSEC
    |   HSEC-MapServerInfo.json
    |
    +---HSECAreaStatus
    |       HSECAreaStatus-chunk-1.json
    |       HSECAreaStatus-ObjectIds.json
    |       HSECAreaStatus.geojson
    |
    +---HSECPipelineStatus
    |       HSECPipelineStatus-chunk-1.json
    |       HSECPipelineStatus-ObjectIds.json
    |       HSECPipelineStatus.geojson
    |
    \---HSECPointStatus
            HSECPointStatus-chunk-1.json
            HSECPointStatus-ObjectIds.json
            HSECPointStatus.geojson

#### Downloading data from a specifc layer(s) of a map service.  Include the layer names separated by commas.#### 

`armcop -m https://sampleserver6.arcgisonline.com/arcgis/rest/services/Energy/HSEC/MapServer -d d:\tmp\HSEC -l "HSEC Pipeline Status"`

After the the program is finished, the output directory will look like:

    D:\TMP\HSEC
    |   HSEC-MapServerInfo.json
    |
    \---HSECPipelineStatus
            HSECPipelineStatus-chunk-1.json
            HSECPipelineStatus-ObjectIds.json
            HSECPipelineStatus.geojson



---
## ToDo Items ##
- Refactor Program.cs code.
- Produce Error ID Output.
- Accept Layer ID CSV 
- Add option to remove chunk files after the final merge is complete.




