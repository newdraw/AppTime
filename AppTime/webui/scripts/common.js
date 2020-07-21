function textwidth(text, fontfamily, fontsize) {
    if (document.body.clientWidth == 0) {
        return parseFloat(fontsize) / 2;
    }
    var div = $('<span style="font-family:' + fontfamily + ';font-size:' + fontsize + '">' + text + '</span>');
    div.appendTo($(document.body));
    var result = div[0].offsetWidth;
    div.remove();
    return result;
}

function strwidth(str) {
    if (!str) {
        return 0;
    }

    if (str.constructor != String) {
        str = str.toString();
    }

    var result = 0;
    var len = str.length;
    for (var i = 0; i < len; i++) {
        var c = str.charCodeAt(i);
        if (c > 255) {
            result += 2;
        }
        else {
            result += 1;
        }
    }
    return result;
}

function initGrid(grid, datasrc, args, optsHandler)
{
    if(!args) {
        args = [];
    }
    Utils.gridColumns(datasrc, args, function(columns){
        var cols = [];
        cols.push({field:'checked', checkbox:true});

        for(var i = 0; i < columns.length; i++)
        {
            var field = columns[i];
            if (field.startsWith('_')) {
                continue;
            }

            cols.push({ field: field, title: field, align: 'center', resizable:false });
        }
        console.log(cols);
        var opts = {
            idField: '_id',
            columns: [cols],
            pageSize : 50,
            pageList: [20,50,100,500],
            url:"../data/Utils.gridData?args=" + encodeURI(JSON.stringify([datasrc, args]))
        };
        if(optsHandler) {
            optsHandler(opts);
        }

        grid.datagrid(opts);
    });

}
 
function toArray(obj) {
    var result = [];
    $.each(obj, function (i, v) {
        result.push(v);
    });
    return result;
}; 
 
Array.prototype.where = function (selector) {
    var result = [];
    $.each(this, function (i, v) {
        if (selector(v)) {
            result.push(v);
        }
    });
    return result;
};

Array.prototype.unionAll = function (selector) {
    var result = [];
    $.each(this, function (i, v) {
        $.each(selector(v), function (i, v) {
            result.push(v);
        }); 
    });
    return result;
};


Array.prototype.first = function (selector) {
    if (this.length == 0) {
        return null;
    }

    if (!selector) {
        return this[0];
    }

    for (var i = 0; i < this.length; i++) {
        var item = this[i];
        if (selector(item)) {
            return item;
        }
    }
    return null;
}

function clone(obj) {
    return JSON.parse(JSON.stringify(obj));
}