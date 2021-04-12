const fetch = require('node-fetch');
const shell = require('shelljs');
const fs = require('fs');
const os = require('os');
if (os.platform() !== 'linux') {
    console.error('Requires Linux');
    process.exit(1);
}
const args = require('minimist')(process.argv.slice(2));
if (args.length < 4) {
    console.error('provide args: --image=image.png --username=user --password=pass123 --avatarid=avtr_id');
    process.exit(1);
}

//Linux dependencies: openssl and base64
var apiUrl = 'https://api.vrchat.cloud/api/1';
var imageFile = args['image'];
var username = args['username'];
var password = args['password'];
var avatarId = args['avatarid'];

var initHeaders = {
    'accept': '*/*',
    'Content-Type': 'application/json;charset=utf-8',
    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/89.0.4389.114 Safari/537.36'
};

function parseCookies(response) {
    var raw = response.headers.raw()['set-cookie'];
    return raw.map((entry) => {
        var parts = entry.split(';');
        var cookiePart = parts[0];
        return cookiePart;
    }).join(';');
};

var extractFileId = (s) => {
    var match = String(s).match(/file_[0-9A-Za-z-]+/);
    if (match) {
        return match[0];
    } else {
        console.error('Extract image file id failed');
        process.exit(1);
    }
};

var genMd5 = async function (file) {
    var md5 = await shell.exec(`openssl md5 -binary ${file} | base64`).stdout.trim();
    return md5;
};

var getFileSize = function (file) {
    var fileInfo = fs.statSync(file);
    var fileSizeInBytes = fileInfo.size;
    return fileSizeInBytes;
};

var getConfig = async function () {
    var res = await fetch(`${apiUrl}/config`, {
        method: 'GET'
    });
    var json = await res.json();
    return json;
};

var login = async function (apiKey) {
    var user = encodeURIComponent(username);
    var pass = encodeURIComponent(password);
    var auth = Buffer.from(`${user}:${pass}`).toString('base64');
    var res = await fetch(`${apiUrl}/auth/user?apiKey=${apiKey}`, {
        method: 'GET',
        headers: {
            Authorization: `Basic ${auth}`,
            headers: initHeaders
        }
    });
    if (res.status !== 200) {
        console.error('Login error: ', res.statusText);
        process.exit(1);
    }
    return res;
};

var getAvatar = async function (headers) {
    var res = await fetch(`${apiUrl}/avatars/${avatarId}`, {
        method: 'GET',
        headers
    });
    var json = await res.json();
    if (res.status !== 200) {
        console.error('Avatar error: ', res.statusText);
        process.exit(1);
    }
    return json;
};

var failCleanup = async function (headers, fileId) {
    var res = await fetch(`${apiUrl}/file/${fileId}`, {
        method: 'GET',
        headers
    });
    var json = await res.json();
    var fileVersion = json.versions[json.versions.length - 1].version;
    var res = await fetch(`${apiUrl}/file/${fileId}/${fileVersion}/file/finish`, {
        method: 'PUT',
        headers
    });
    var res = await fetch(`${apiUrl}/file/${fileId}/${fileVersion}/signature/finish`, {
        method: 'PUT',
        headers
    });
    console.error('Caught error and ran cleanup');
    process.exit(1);
};

var program = async function () {
    var config = await getConfig();
    var apiKey = config.clientApiKey;
    var loginData = await login(apiKey);
    var cookie = parseCookies(loginData);
    var headers = {
        cookie,
        'Content-Type': 'application/json;charset=utf-8',
        'User-Agent': initHeaders['User-Agent']
    };
    var avatarData = await getAvatar(headers);
    var imageUrl = avatarData.imageUrl;
    var fileId = extractFileId(avatarData.imageUrl);
    console.log(fileId);
    var rawFile = fs.readFileSync(imageFile, function (err, data) {
        if (err) { throw err; }
        return data;
    });
    var fileMd5 = await genMd5(imageFile);
    var fileSizeInBytes = getFileSize(imageFile);
    var signatureMd5 = fileMd5;
    var signatureSizeInBytes = Math.floor(Math.random() * (10000 - 500 + 1)) + 500;

    //start upload POST
    var params = {
        fileMd5,
        fileSizeInBytes,
        signatureMd5,
        signatureSizeInBytes
    };
    var res = await fetch(`${apiUrl}/file/${fileId}`, {
        method: 'POST',
        headers,
        body: JSON.stringify(params)
    });
    var json = await res.json();
    if (res.status !== 200) {
        console.error('uploadPOST error: ', res.statusText);
        failCleanup(headers, fileId);
        process.exit(1);
    }
    var fileVersion = json.versions[json.versions.length - 1].version;

    //start image upload PUT
    var res = await fetch(`${apiUrl}/file/${fileId}/${fileVersion}/file/start`, {
        method: 'PUT',
        headers
    });
    if (res.status !== 200) {
        console.error('start image PUT error: ', res.statusText);
        failCleanup(headers, fileId);
        process.exit(1);
    }
    var json = await res.json();
    var awsUrl = json.url;

    //AWS image upload
    var res = await fetch(awsUrl, {
        method: 'PUT',
        body: rawFile,
        headers: {
            'User-Agent': initHeaders['User-Agent'],
            'Content-MD5': fileMd5,
            'Content-type': 'image/png',
        }
    });
    if (res.status !== 200) {
        console.error('AWS image upload error: ', res.statusText);
        failCleanup(headers, fileId);
        process.exit(1);
    }

    //finish image upload PUT
    var finishBody = {
        maxParts: 0,
        nextPartNumber: 0
    };
    var res = await fetch(`${apiUrl}/file/${fileId}/${fileVersion}/file/finish`, {
        method: 'PUT',
        headers,
        body: JSON.stringify(finishBody)
    });
    if (res.status !== 200) {
        console.error('finish image PUT error: ', res.statusText);
        process.exit(1);
    }

    //finish signature upload PUT
    var res = await fetch(`${apiUrl}/file/${fileId}/${fileVersion}/signature/finish`, {
        method: 'PUT',
        headers,
        body: JSON.stringify(finishBody)
    });
    if (res.status !== 200) {
        console.error('finish signature PUT error: ', res.statusText);
        process.exit(1);
    }

    //update current image PUT
    var setImageBody = {
        id: avatarId,
        imageUrl: `https://api.vrchat.cloud/api/1/file/${fileId}/${fileVersion}/file`
    };
    var res = await fetch(`${apiUrl}/avatars/${avatarId}`, {
        method: 'PUT',
        headers,
        body: JSON.stringify(setImageBody)
    });
    if (res.status !== 200) {
        console.error('update current image PUT error: ', res.statusText);
        process.exit(1);
    }
    var json = await res.json();

    //done
    if (json.imageUrl === imageUrl) {
        console.error('failed to change image');
    } else {
        console.log('success!');
    }
};
program();
