window.downloadFile = function (fileName, base64Content, mimeType) {
    const link = document.createElement('a');
    const resolvedMimeType = mimeType || 'text/csv';
    link.href = 'data:' + resolvedMimeType + ';base64,' + base64Content;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

window.exportHistoryPdf = function (fileName, rows, meta) {
    if (!window.jspdf || !window.jspdf.jsPDF) {
        throw new Error('jsPDF not loaded');
    }

    const doc = new window.jspdf.jsPDF({ orientation: 'landscape', unit: 'pt', format: 'a4' });

    const title = (meta && meta.title) ? meta.title : 'Transaction History Report';
    const generatedOn = (meta && meta.generatedOn) ? meta.generatedOn : '';
    const dateRange = (meta && meta.dateRange) ? meta.dateRange : '';

    const totalTransactions = (meta && meta.totalTransactions != null) ? String(meta.totalTransactions) : '';
    const totalRevenue = (meta && meta.totalRevenue != null) ? String(meta.totalRevenue) : '';
    const totalDeposits = (meta && meta.totalDeposits != null) ? String(meta.totalDeposits) : '';

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(16);
    doc.text(title, 40, 40);

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(10);
    if (generatedOn) doc.text('Generated on: ' + generatedOn, 40, 60);
    if (dateRange) doc.text('Date range: ' + dateRange, 40, 74);

    const head = [[
        'Customer Name',
        'Service Type',
        'Barber',
        'Total Price (₱)',
        'Deposit (₱)',
        'Date & Time',
        'Status'
    ]];

    const body = (rows || []).map(r => [
        r.customerName || '',
        r.serviceType || '',
        r.barberName || '',
        r.totalPrice || '',
        r.deposit || '',
        r.dateTime || '',
        r.status || ''
    ]);

    const startY = 96;
    doc.autoTable({
        head,
        body,
        startY,
        theme: 'grid',
        headStyles: { fillColor: [249, 250, 251], textColor: [107, 114, 128], fontStyle: 'bold' },
        styles: { font: 'helvetica', fontSize: 9, textColor: [31, 41, 55], cellPadding: 6 },
        columnStyles: {
            3: { halign: 'right' },
            4: { halign: 'right' }
        },
        margin: { left: 40, right: 40 }
    });

    const endY = (doc.lastAutoTable && doc.lastAutoTable.finalY) ? doc.lastAutoTable.finalY : startY;
    const summaryY = Math.min(endY + 22, 560);

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(11);
    doc.text('Summary', 40, summaryY);

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(10);
    doc.text('Total Transactions: ' + totalTransactions, 40, summaryY + 16);
    doc.text('Total Revenue (₱): ' + totalRevenue, 40, summaryY + 30);
    doc.text('Total Deposits (₱): ' + totalDeposits, 40, summaryY + 44);

    doc.save(fileName);
};
