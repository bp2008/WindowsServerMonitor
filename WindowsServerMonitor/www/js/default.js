var siteApi = (function ($)
{
	$(function ()
	{
		LoadInitialData();
	});
	var dataLoadStarted = false;
	var lastTime = 0;
	function LoadInitialData()
	{
		if (dataLoadStarted)
			return;
		dataLoadStarted = true;
		ExecAPI("getCounterRecords", function (response)
		{
			if (response.result === "success")
			{
				//for (var i = 0; i < response.collections.length; i++)
				//{
				//	var collection = response.collections[i];
				//	collection.name;
				//	collection.scale;
				//	var values = collection.values;
				//	//values[0].time;
				//	//values[0].value;
				//}
				InitializeGraphableData(response);

				RenderChart();

				RefreshDataAfterTimeout();
			}
			else
				SimpleDialog.Text(response.error);
		}, function (jqXHR, textStatus, errorThrown)
			{
				console.log(jqXHR.ErrorMessageHtml);
				SimpleDialog.html(jqXHR.ErrorMessageHtml);
			});
	}
	function RefreshDataAfterTimeout()
	{
		setTimeout(RefreshData, 1000);
	}
	function RefreshData()
	{
		if (document.visibilityState === 'visible' && $('#DataGraph').is(':visible'))
		{
			ExecAPI("getCounterRecords?time=" + lastTime, function (response)
			{
				if (response.result === "success")
				{
					{ // Build error messages array
						$("#titleExtension").text(": " + response.machineName);
						var sb = [];
						sb.push('<div class="errMsgs">');
						for (var i = 0; i < response.collections.length; i++)
						{
							var c = response.collections[i];
							if (c.error)
							{
								sb.push('<div class="errMsg">');
								sb.push(EscapeHTML(c.error));
								sb.push('</div>');
							}
						}
						sb.push('</div>');
					}
					$("#DataMessages").html(sb.join(''));

					UpdateGraphableData(response.collections);
					UpdateChart();

					RefreshDataAfterTimeout();
				}
				else
					console.log(response.error);
			}, function (jqXHR, textStatus, errorThrown)
				{
					console.log(jqXHR.ErrorMessageHtml);
					RefreshDataAfterTimeout();
				});
		}
		else
			RefreshDataAfterTimeout();
	}
	///////////////////////////////////////////////////////////////
	// Chart //////////////////////////////////////////////////////
	///////////////////////////////////////////////////////////////
	var myChart;
	var graphableData;
	function InitializeGraphableData(response)
	{
		graphableData = new Array();
		for (var i = 0; i < response.collections.length; i++)
		{
			var collection = response.collections[i];
			graphableData.push(GetSeries(collection.name));
		}
		UpdateGraphableData(response.collections);
	}
	function GetSeries(name)
	{
		return {
			type: 'line',
			showSymbol: false,
			hoverAnimation: false,
			name: name,
			//areaStyle: {
			//	normal: {
			//		color: color,
			//		opacity: 0.25
			//	}
			//},
			//lineStyle: {
			//	normal: {
			//		width: 1,
			//		color: color,
			//		shadowColor: "#666666",
			//		shadowBlur: 0,
			//		shadowOffsetX: 0.5,
			//		shadowOffsetY: 0.5
			//	}
			//},
			sampling: "average",
			data: new Array()
		};
	}
	function UpdateGraphableData(collections)
	{
		if (!graphableData || graphableData.length === 0)
			return;
		for (var i = 0; i < collections.length; i++)
		{
			var collection = collections[i];
			UpdateDataArray(collection);
		}
	}
	function FindGraphData(name)
	{
		for (var i = 0; i < graphableData.length; i++)
			if (graphableData[i].name === name)
				return graphableData[i].data;
		return null;
	}
	function UpdateDataArray(collection)
	{
		var data = FindGraphData(collection.name);
		if (data === null)
			return;
		var maxAge = 60000 * 60 * 24; // 1 day in milliseconds
		var newestAdded = 0;
		for (var i = collection.values.length - 1; i >= 0; i--)
		{
			var record = collection.values[i];
			var x = new Date(record.Time);
			var y = record.Value * collection.scale;
			data.push({ name: x.toString(), value: [x, y] });
			if (record.Time > newestAdded)
				newestAdded = lastTime = record.Time;
		}
		if (newestAdded !== 0)
		{
			var ageCutoff = newestAdded - maxAge;
			var removeCount = 0;
			for (var n = 0; n < data.length; n++)
			{
				if (data[n].value[0] < ageCutoff)
					removeCount++;
				else
					break;
			}
			if (removeCount > 0)
				data.splice(0, removeCount);
		}
	}
	function UpdateChart()
	{
		myChart.setOption(
			{
				animation: true,
				series: graphableData
			});
	}
	/**
	 * Generate HTML for a tooltip for the current position in the graph.
	 * @param {any} params parameters from eCharts
	 * @returns {string} HTML for a tooltip.
	 */
	function GetTooltip(params)
	{
		var time = params[0].value[0].getTime();
		var sb = new Array();
		for (var s = 0; s < graphableData.length; s++)
		{
			var series = graphableData[s];
			var idx = binarySearch(series.data, time);
			if (idx < 0)
				idx *= -1; // This index will be the nearest match.
			if (idx < series.data.length)
				sb.push('<div>' + series.name + ': ' + series.data[idx].value[1].toFixedNoE(2) + '</div>');
		}
		return sb.join('');
	}
	function RenderChart()
	{
		var $DataGraph = $('#DataGraph');
		myChart = echarts.init($DataGraph[0]);

		// specify chart configuration item and data
		var option = {
			title: {
				//text: 'My Chart'
			},
			tooltip: {
				trigger: 'axis',
				formatter: GetTooltip,
				axisPointer: {
					animation: false
				}
			},
			grid: {
				left: 60
				, right: 60
				, bottom: 100
			},
			xAxis: {
				type: 'time',
				splitLine: {
					show: true
				},
				splitNumber: 10
			},
			yAxis: {
				name: 'Magnitude',
				type: 'value',
				//boundaryGap: [0, '100%'],
				splitLine: {
					show: true
				}
			},
			dataZoom: [{ type: 'inside' }, { type: 'slider' }],
			legend: {
				orient: 'horizontal'
			},
			series: graphableData,
			animation: false
		};

		// use configuration item and data specified to show chart
		myChart.setOption(option);
		$(window).resize(function ()
		{
			myChart.resize();
		});
	}
	///////////////////////////////////////////////////////////////
	// Performance Counter Categories /////////////////////////////
	///////////////////////////////////////////////////////////////
	this.LoadPCCats = function ()
	{
		$("#PCCats").text('Loading…');
		ExecAPI("getPerformanceCounterCategories", function (response)
		{
			if (response.result === "success")
			{
				$("#PCCats").html(BuildPCCatsHtml(response.categories));
				$("#PCCats .pcCat").on('click', LoadPCCounter);
			}
			else
				SimpleDialog.text(response.error);
		}, function (jqXHR, textStatus, errorThrown)
			{
				console.log(jqXHR.ErrorMessageHtml);
				SimpleDialog.html(jqXHR.ErrorMessageHtml);
			});
	}
	function BuildPCCatsHtml(cats)
	{
		var sb = [];
		for (var i = 0; i < cats.length; i++)
			BuildPCCat(sb, cats[i]);
		return sb.join("");
	}
	function BuildPCCat(sb, cat)
	{
		sb.push('<div class="pcCat" title="');
		sb.push(EscapeHTML(cat.type));
		sb.push('" myName="');
		sb.push(EscapeHTML(cat.name));
		sb.push('">');
		sb.push('<div class="pcCatName">');
		sb.push(EscapeHTML(cat.name));
		sb.push('</div>');
		sb.push('<div class="pcCatHelp">');
		sb.push(EscapeHTML(cat.help));
		sb.push('</div>');
		sb.push('</div>');
	}
	///////////////////////////////////////////////////////////////
	// Performance Counters ///////////////////////////////////////
	///////////////////////////////////////////////////////////////
	function LoadPCCounter(e)
	{
		var $details = $('<div class="pcCatDetails">Loading…</div>');
		var $cat = $(e.currentTarget);
		$cat.off('click');
		$cat.on('click', function ()
		{
			$details.remove();
			$cat.off('click');
			$cat.on('click', LoadPCCounter);
		});
		$cat.after($details);
		var catName = $cat.attr('myName');
		ExecAPI("PerformanceCounterCategoryDetails/" + encodeURIComponent(catName), function (response)
		{
			if (response.result === "success")
				$details.html(BuildPCCatDetailsHtml(response.details));
			else
				$details.text(response.error);
		}, function (jqXHR, textStatus, errorThrown)
			{
				$details.html(jqXHR.ErrorMessageHtml);
			});
	}
	function BuildPCCatDetailsHtml(details)
	{
		var sb = [];

		if (details.instances)
		{
			sb.push('<div class="pcInstances">');
			sb.push('<div class="pcInstancesHeader">Instances:</div>');
			details.instances.sort();
			for (var i = 0; i < details.instances.length; i++)
			{
				sb.push('<div class="pcInstanceName">');
				sb.push('<div>'); // Extra wrapping div causes double and triple click selections to not extend into adjacent names.
				sb.push(EscapeHTML(details.instances[i]));
				sb.push('</div>');
				sb.push('</div>');
			}
			sb.push('</div>');
		}

		if (details.counters)
			BuildPCCountersListHtml(sb, details.counters);
		else
			sb.Append("<div>No counters were found under this type!</div>");

		return sb.join("");
	}
	function BuildPCCountersListHtml(sb, counters)
	{
		sb.push('<div class="pcCounters">');
		sb.push('<div class="pcCountersHeader">Counters:</div>');
		counters.sort(function (a, b) { return strcmp(a.name, b.name); });
		for (var i = 0; i < counters.length; i++)
		{
			BuildPCCounterInstance(sb, counters[i]);
		}
		sb.push('</div>');
	}
	function BuildPCCounterInstance(sb, counter)
	{
		sb.push('<div class="pcCounter">');
		sb.push('<div class="pcCounterName">');
		sb.push(EscapeHTML(counter.name));
		sb.push('</div>');
		sb.push('<div class="pcCounterHelp">');
		sb.push(EscapeHTML(counter.help));
		sb.push('</div>');
		sb.push('</div>');
	}

	///////////////////////////////////////////////////////////////
	// Misc ///////////////////////////////////////////////////////
	///////////////////////////////////////////////////////////////
	String.prototype.startsWith = function (prefix)
	{
		return this.lastIndexOf(prefix, 0) === 0;
	};
	String.prototype.endsWith = function (suffix)
	{
		var idx = this.lastIndexOf(suffix);
		return idx !== -1 && idx === (this.length - suffix.length);
	};
	Number.prototype.toFixedLoose = function (decimals)
	{
		return parseFloat(this.toFixed(decimals));
	};
	function MB_To_MiB(MB, fixedPrecision)
	{
		var B = MB * 1000000;
		var MiB = B / 1048576;
		if (typeof fixedPrecision === "number")
			return MiB.toFixed(fixedPrecision);
		else
			return MiB;
	}
	function msToTimeString(totalMs)
	{
		var ms = totalMs % 1000;
		var totalS = totalMs / 1000;
		var totalM = totalS / 60;
		var totalH = totalM / 60;
		var totalD = totalH / 24;
		//var s = Math.floor(totalS) % 60;
		var m = Math.floor(totalM) % 60;
		var h = Math.floor(totalH) % 24;
		var d = Math.floor(totalD);

		var retVal = "";
		if (d !== 0)
			retVal += d + " day" + (d === 1 ? "" : "s") + ", ";
		if (d !== 0 || h !== 0)
			retVal += h + " hour" + (h === 1 ? "" : "s") + ", ";
		retVal += m + " minute" + (m === 1 ? "" : "s");
		return retVal;
	}
	function msToRoughTimeString(totalMs)
	{
		var ms = totalMs % 1000;
		var totalS = totalMs / 1000;
		var totalM = totalS / 60;
		var totalH = totalM / 60;
		var totalD = totalH / 24;
		var s = Math.floor(totalS) % 60;
		var m = Math.floor(totalM) % 60;
		var h = Math.floor(totalH) % 24;
		var d = Math.floor(totalD);

		if (d !== 0)
			return d + " day" + (d === 1 ? "" : "s");
		if (h !== 0)
			return h + " hour" + (h === 1 ? "" : "s");
		if (m !== 0)
			return m + " minute" + (m === 1 ? "" : "s");
		return s + " second" + (s === 1 ? "" : "s");
	}
	function formatBytes(bytes)
	{
		if (bytes === 0) return '0 bps';
		var negative = bytes < 0;
		if (negative)
			bytes = -bytes;
		var bits = bytes * 8;
		var k = 1000,
			dm = typeof decimals !== "undefined" ? decimals : 1,
			sizes = ['b', 'Kb', 'Mb', 'Gb', 'Tb', 'Pb', 'Eb', 'Zb', 'Yb'],
			i = Math.floor(Math.log(bits) / Math.log(k));
		var highlight;
		if (bits > 100000000) // > 100 Mbps
			highlight = "extreme";
		else if (bits > 20000000) // > 20 Mbps
			highlight = "veryhigh";
		else if (bits > 1000000) // > 1 Mbps
			highlight = "high";
		else if (bits > 50000) // > 50 Kbps
			highlight = "med";
		else // < 50 Kbps
			highlight = "low";
		return '<span style="display: none;">' + bytes + ' </span><span class="bits_' + highlight + '">' + (negative ? '-' : '') + (bits / Math.pow(k, i)).toFloat(dm) + " " + sizes[i] + 'ps</span>';
	}
	function bytesToKilobits(bytes)
	{
		return bytes / 125;
	}
	function formatBytes2(bytes, decimals)
	{
		if (bytes === 0) return '0B';
		var negative = bytes < 0;
		if (negative)
			bytes = -bytes;
		var k = 1000,
			dm = typeof decimals !== "undefined" ? decimals : 1,
			sizes = ['B', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'],
			i = Math.floor(Math.log(bytes) / Math.log(k));
		return '<span style="display: none;">' + bytes + ' </span><span class="bytes_' + sizes[i] + '">' + (negative ? '-' : '') + (bytes / Math.pow(k, i)).toFloat(dm) + " " + sizes[i] + '/s</span>';
	}
	String.prototype.toFloat = function (digits)
	{
		return parseFloat(this.toFixed(digits));
	};
	Number.prototype.toFloat = function (digits)
	{
		return parseFloat(this.toFixed(digits));
	};
	var use24HourTime = false;
	function GetTimeStr(date, includeMilliseconds)
	{
		var ampm = "";
		var hour = date.getHours();
		if (!use24HourTime)
		{
			if (hour === 0)
			{
				hour = 12;
				ampm = " AM";
			}
			else if (hour === 12)
			{
				ampm = " PM";
			}
			else if (hour > 12)
			{
				hour -= 12;
				ampm = " PM";
			}
			else
			{
				ampm = " AM";
			}
		}
		var ms = includeMilliseconds ? ("." + date.getMilliseconds()) : "";

		var str = hour.toString().padLeft(2, '0') + ":" + date.getMinutes().toString().padLeft(2, '0') + ":" + date.getSeconds().toString().padLeft(2, '0') + ms + ampm;
		return str;
	}
	String.prototype.padLeft = function (len, c)
	{
		var pads = len - this.length;
		if (pads > 0)
		{
			var sb = [];
			var pad = c || "&nbsp;";
			for (var i = 0; i < pads; i++)
				sb.push(pad);
			sb.push(this);
			return sb.join("");
		}
		return this;

	};
	String.prototype.padRight = function (len, c)
	{
		var pads = len - this.length;
		if (pads > 0)
		{
			var sb = [];
			sb.push(this);
			var pad = c || "&nbsp;";
			for (var i = 0; i < pads; i++)
				sb.push(pad);
			return sb.join("");
		}
		return this;
	};
	String.prototype.setLength = function (len, paddingChar)
	{
		if (this.length === len)
			return this;
		else if (this.length > len)
			return this.substr(0, len);
		else
			return this.padRight(len, paddingChar);
	};
	Number.prototype.padLeft = function (len, c)
	{
		return this.toString().padLeft(len, c);
	};
	Number.prototype.padRight = function (len, c)
	{
		return this.toString().padRight(len, c);
	};
	var escape = document.createElement('textarea');
	var EscapeHTML = function (html)
	{
		escape.textContent = html;
		return escape.innerHTML;
	};
	var UnescapeHTML = function (html)
	{
		escape.innerHTML = html;
		return escape.textContent;
	};
	function strcmp(a, b)
	{
		if (a < b) return -1;
		if (a > b) return 1;
		return 0;
	}
	/**
	 * THIS METHOD HAS BEEN MODIFIED TO OPERATE ON eCharts SERIES DATA
	 * Performs a binary search on the provided sorted list and returns the index of the item if found. If it can't be found it'll return -1.
	 *
	 * @param {*[]} list Items to search through.
	 * @param {*} item The item to look for.
	 * @return {Number} The index of the item if found, -1 if not.
	 */
	function binarySearch(list, item)
	{
		var min = 0;
		var max = list.length - 1;
		var guess;

		while (min <= max)
		{
			guess = Math.floor((min + max) / 2);

			if (list[guess].value[0].getTime() === item)
			{
				return guess;
			}
			else
			{
				if (list[guess].value[0].getTime() < item)
				{
					min = guess + 1;
				}
				else
				{
					max = guess - 1;
				}
			}
		}

		return -1 * guess;
	}
	Number.prototype.toFixedNoE = function (digits)
	{
		var str = this.toFixed(digits);
		if (str.indexOf('e+') < 0)
			return str;

		// if number is in scientific notation, pick (b)ase and (p)ower
		return str.replace('.', '').split('e+').reduce(function (p, b)
		{
			return p + Array(b - p.length + 2).join(0);
		}) + (digits > 0 ? ('.' + Array(digits + 1).join(0)) : '');
	};

	function ExecAPI(cmd, callbackSuccess, callbackFail)
	{
		var reqUrl = "api/" + cmd;
		$.ajax({
			type: 'POST',
			url: reqUrl,
			contentType: "text/plain",
			data: "",
			dataType: "json",
			success: function (data)
			{
				if (callbackSuccess)
					callbackSuccess(data);
			},
			error: function (jqXHR, textStatus, errorThrown)
			{
				if (!jqXHR)
					jqXHR = { status: 0, statusText: "No jqXHR object was created" };
				jqXHR.OriginalURL = reqUrl;
				jqXHR.ErrorMessageHtml = 'Response: ' + jqXHR.status + ' ' + jqXHR.statusText + '<br>Status: ' + textStatus + '<br>Error: ' + errorThrown + '<br>URL: ' + reqUrl;
				if (callbackFail)
					callbackFail(jqXHR, textStatus, errorThrown);
			}
		});
	}
	return this;
})(jQuery);