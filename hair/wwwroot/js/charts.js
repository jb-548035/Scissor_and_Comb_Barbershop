// Chart initialization functions
window.initSalesChart = function(dates, salesData) {
    if (typeof echarts === 'undefined') {
        console.error('ECharts not loaded');
        return;
    }

    var salesChart = echarts.init(document.getElementById('salesChart'));
    var salesOption = {
        color: ['#80FFA5', '#00DDFF', '#37A2FF', '#FF0087', '#FFBF00'],
        tooltip: {
            trigger: 'axis',
            axisPointer: {
                type: 'cross',
                label: {
                    backgroundColor: '#6a7985'
                }
            }
        },
        xAxis: [
            {
                type: 'category',
                boundaryGap: false,
                data: dates
            }
        ],
        yAxis: [
            {
                type: 'value',
                axisLabel: {
                    formatter: '₱{value}'
                }
            }
        ],
        series: [
            {
                name: 'Sales',
                type: 'line',
                smooth: true,
                lineStyle: {
                    width: 0
                },
                showSymbol: false,
                areaStyle: {
                    opacity: 0.8,
                    color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
                        {
                            offset: 0,
                            color: 'rgb(128, 255, 165)'
                        },
                        {
                            offset: 1,
                            color: 'rgb(1, 191, 236)'
                        }
                    ])
                },
                emphasis: {
                    focus: 'series'
                },
                data: salesData
            }
        ]
    };
    
    salesChart.setOption(salesOption);
    
    window.addEventListener('resize', function() {
        salesChart.resize();
    });
};

window.initCustomerChart = function(dates, customerData) {
    if (typeof echarts === 'undefined') {
        console.error('ECharts not loaded');
        return;
    }

    var customerChart = echarts.init(document.getElementById('customerChart'));
    var customerOption = {
        color: ['#80FFA5', '#00DDFF', '#37A2FF', '#FF0087', '#FFBF00'],
        tooltip: {
            trigger: 'axis',
            axisPointer: {
                type: 'cross',
                label: {
                    backgroundColor: '#6a7985'
                }
            }
        },
        xAxis: [
            {
                type: 'category',
                boundaryGap: false,
                data: dates
            }
        ],
        yAxis: [
            {
                type: 'value'
            }
        ],
        series: [
            {
                name: 'Customers',
                type: 'line',
                smooth: true,
                lineStyle: {
                    width: 0
                },
                showSymbol: false,
                areaStyle: {
                    opacity: 0.8,
                    color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
                        {
                            offset: 0,
                            color: 'rgb(55, 162, 255)'
                        },
                        {
                            offset: 1,
                            color: 'rgb(116, 21, 219)'
                        }
                    ])
                },
                emphasis: {
                    focus: 'series'
                },
                data: customerData
            }
        ]
    };
    
    customerChart.setOption(customerOption);
    
    window.addEventListener('resize', function() {
        customerChart.resize();
    });
};
