var path = require('path');
const puppeteer = require('puppeteer');
var azure = require('azure-storage');
var https = {};
if (process.env.environment == 'production')
  https = require('https');
else
  https = require('http');

function timeout(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

const timeoutPeriod = process.env.TIMEOUT;
(async () => {
  var options = {
    defaultViewport: {
      width: 1024,
      height: 768
    },
    args: ['--no-sandbox', '--disable-setuid-sandbox'],
    shotSize: {
      width: 'all',
      height: 'all'
    },
    javascriptEnabled: true,
    phantomConfig: { "ssl-protocol": "ANY", 'ignore-ssl-errors': 'true' }
  };
  const browser = await puppeteer.launch(options);
  console.log(process.env.charts);
  var charts = JSON.parse(process.env.charts);
  var time = Date.now();
  console.log(`started at ${new Date().toString()}`)
  var blobService = azure.createBlobService();
  blobService.createContainerIfNotExists(process.env.SCREENSHOTS_CONTAINER, { publicAccessLevel: 'blob' }, async function (error, result, response) {
    if (!error) {
      //go through each chart and create a screenshot
      await Promise.all(charts.map(function (chart) {
        return downloadImage(browser, chart, time, blobService);
      }));
      console.log(process.env.FUNCTION_URL + `&timestamp=${time}`);

      //invoke our HTTP function with a GET request which has the timestamp in the query
      https.get(process.env.FUNCTION_URL + `&timestamp=${time}`, function () {
        console.log('request done');
      });
    }
  });
})();

async function downloadImage(browser, chart, timestamp, blobService) {

  //create a new page inside the browser
  const page = await browser.newPage();
  console.log('ChartUrl is ' + chart.chartUrl);
  //open the chart page
  await page.goto(chart.chartUrl);
  //wait for the page to finish loading
  await timeout(timeoutPeriod);
  const tempImage = `${chart.market}-${timestamp}.png`;
  //create the screenshot
  await page.screenshot({ path: path.join(__dirname, 'images', tempImage) });
  return new Promise(function (resolve, reject) {
    blobService.createBlockBlobFromLocalFile(process.env.SCREENSHOTS_CONTAINER, tempImage, path.join(__dirname, '/images', tempImage), function (error, result, response) {
      if (!error) {
        console.log(`file ${tempImage} uploaded to azure storage`);
        resolve(tempImage);
      }
      else {
        console.log(error);
        reject(error);
      }
    });
  });
}
