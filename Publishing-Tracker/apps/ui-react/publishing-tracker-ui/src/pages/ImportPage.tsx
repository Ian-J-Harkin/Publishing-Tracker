import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { importService } from '../services/importService';
import { ImportJob, ColumnMapping } from '../types/import';

const ImportPage = () => {
    const navigate = useNavigate();
    const [file, setFile] = useState<File | null>(null);
    const [mapping, setMapping] = useState<ColumnMapping>({
        bookTitle: '',
        platform: '',
        saleDate: '',
        quantity: '',
        unitPrice: '',
        royalty: '',
        revenue: '',
        currency: '',
        orderId: ''
    });
    const [headers, setHeaders] = useState<string[]>([]);
    const [step, setStep] = useState<'upload' | 'mapping' | 'summary'>('upload');
    const [error, setError] = useState<string | null>(null);
    const [summary, setSummary] = useState<ImportJob | null>(null);

    const autoMapHeaders = (availableHeaders: string[]) => {
        const newMapping = { ...mapping };
        availableHeaders.forEach(header => {
            const h = header.toLowerCase();
            if (h.includes('title') || h.includes('book')) newMapping.bookTitle = header;
            if (h.includes('platform')) newMapping.platform = header;
            if (h.includes('date')) newMapping.saleDate = header;
            if (h.includes('qty') || h.includes('quantity')) newMapping.quantity = header;
            if (h.includes('price')) newMapping.unitPrice = header;
            if (h.includes('royalty')) newMapping.royalty = header;
            if (h.includes('revenue') || h.includes('total')) newMapping.revenue = header;
            if (h.includes('currency')) newMapping.currency = header;
            if (h.includes('order') || h.includes('id')) newMapping.orderId = header;
        });
        setMapping(newMapping);
    };

    const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        if (e.target.files) {
            setFile(e.target.files[0]);
            setError(null);
        }
    };

    const handleUpload = async () => {
        if (file) {
            try {
                const data = await importService.uploadFile(file);
                setHeaders(data.headers);
                autoMapHeaders(data.headers);
                setStep('mapping');
            } catch {
                setError('Failed to upload file. Ensure it is a valid CSV.');
            }
        }
    };

    const handleMappingChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
        const { name, value } = e.target;
        setMapping(prevMapping => ({ ...prevMapping, [name]: value }));
    };

    const handleProcess = async () => {
        if (file) {
            try {
                const result = await importService.processFile(file.name, mapping);
                setSummary(result);
                setStep('summary');
            } catch {
                setError('Processing failed. Please check your column mappings.');
            }
        }
    };

    // Helper to render the Progress Stepper
    const renderStepper = () => (
        <div style={pageStyles.stepper}>
            {['upload', 'mapping', 'summary'].map((s, idx) => (
                <div key={s} style={{ display: 'flex', alignItems: 'center' }}>
                    <div style={{
                        ...pageStyles.stepDot,
                        backgroundColor: step === s ? 'var(--primary)' : (idx < ['upload', 'mapping', 'summary'].indexOf(step) ? '#10b981' : '#e2e8f0'),
                        color: 'white'
                    }}>{idx + 1}</div>
                    <span style={{
                        marginLeft: '8px',
                        marginRight: '16px',
                        fontWeight: step === s ? '600' : '400',
                        color: step === s ? 'var(--text-main)' : 'var(--text-muted)'
                    }}>
                        {s.charAt(0).toUpperCase() + s.slice(1)}
                    </span>
                </div>
            ))}
        </div>
    );

    return (
        <div style={{ maxWidth: '900px', margin: '0 auto' }}>
            <h1>Import Sales Data</h1>
            {renderStepper()}

            <div className="card" style={{ padding: '2.5rem' }}>
                {error && <div style={pageStyles.errorBanner}>{error}</div>}

                {step === 'upload' && (
                    <div style={pageStyles.stepContainer}>
                        <h3>Step 1: Upload CSV File</h3>
                        <p style={{ color: 'var(--text-muted)', marginBottom: '1.5rem' }}>
                            Upload the sales export from your publishing platform (KDP, IngramSpark, etc.)
                        </p>

                        <div style={pageStyles.dropZone}>
                            <input type="file" onChange={handleFileChange} accept=".csv,.txt" style={pageStyles.fileInput} id="file-upload" />
                            <label htmlFor="file-upload" style={pageStyles.fileLabel}>
                                {file ? <strong>Selected: {file.name}</strong> : "Click to browse or drag your CSV here"}
                            </label>
                        </div>

                        <div style={pageStyles.actions}>
                            <button className="btn-primary" onClick={handleUpload} disabled={!file}>
                                Next: Map Columns
                            </button>
                        </div>
                    </div>
                )}

                {step === 'mapping' && (
                    <div style={pageStyles.stepContainer}>
                        <h3>Step 2: Map Columns</h3>
                        <p style={{ color: 'var(--text-muted)', marginBottom: '1.5rem' }}>
                            Tell us which column header in your CSV matches our required fields.
                        </p>

                        <div style={pageStyles.grid}>
                            {(Object.keys(mapping) as Array<keyof ColumnMapping>).map((key) => (
                                <div className="form-group" key={key}>
                                    <label style={{ textTransform: 'capitalize' }}>{key.replace(/([A-Z])/g, ' $1')}</label>
                                    <select
                                        name={key}
                                        value={mapping[key]}
                                        onChange={handleMappingChange}
                                        style={pageStyles.select}
                                    >
                                        <option value="">Select CSV Column</option>
                                        {headers.map(h => (
                                            <option key={h} value={h}>{h}</option>
                                        ))}
                                    </select>
                                </div>
                            ))}
                        </div>

                        <div style={pageStyles.actions}>
                            <button onClick={() => setStep('upload')} style={pageStyles.cancelBtn}>Back</button>
                            <button className="btn-primary" onClick={handleProcess}>Process File</button>
                        </div>
                    </div>
                )}

                {step === 'summary' && summary && (
                    <div style={{ ...pageStyles.stepContainer, textAlign: 'center' }}>
                        <div style={pageStyles.successIcon}>✓</div>
                        <h3 style={{ marginBottom: '1.5rem' }}>Import Synchronized</h3>

                        <div style={pageStyles.summaryGrid}>
                            <div style={pageStyles.summaryStat}>
                                <span style={pageStyles.statLabel}>Successful</span>
                                <span style={pageStyles.statValue} className="text-success">{summary.recordsSuccessful}</span>
                            </div>
                            <div style={pageStyles.summaryStat}>
                                <span style={pageStyles.statLabel}>Failed</span>
                                <span style={pageStyles.statValue} className={summary.recordsFailed > 0 ? "text-danger" : ""}>{summary.recordsFailed}</span>
                            </div>
                            <div style={pageStyles.summaryStat}>
                                <span style={pageStyles.statLabel}>Accuracy</span>
                                <span style={pageStyles.statValue}>
                                    {summary.recordsProcessed > 0 ? Math.round((summary.recordsSuccessful / summary.recordsProcessed) * 100) : 0}%
                                </span>
                            </div>
                        </div>

                        {summary.recordsFailed > 0 && (
                            <div style={pageStyles.performanceBarBase}>
                                <div style={{
                                    ...pageStyles.performanceBarFill,
                                    width: `${(summary.recordsSuccessful / summary.recordsProcessed) * 100}%`
                                }} />
                            </div>
                        )}

                        {summary.errorLog && (
                            <div style={pageStyles.errorLogBox}>
                                <div style={pageStyles.diagnosticHeader}>Diagnostic Audit Trail</div>
                                <pre style={pageStyles.errorPre}>{summary.errorLog}</pre>
                            </div>
                        )}
                        <div style={pageStyles.actions}>
                            <button className="btn-primary" onClick={() => navigate('/sales')}>Go to Sales Ledger</button>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
};

const pageStyles = {
    stepper: { display: 'flex', marginBottom: '2rem', padding: '1rem 0' },
    stepDot: { width: '28px', height: '28px', borderRadius: '50%', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '0.8rem', fontWeight: 'bold' },
    stepContainer: { animation: 'fadeIn 0.3s ease' },
    dropZone: {
        border: '2px dashed var(--border)',
        borderRadius: '12px',
        padding: '3rem',
        textAlign: 'center' as const,
        backgroundColor: '#f8fafc',
        cursor: 'pointer',
        position: 'relative' as const
    },
    fileInput: { position: 'absolute' as const, width: '100%', height: '100%', top: 0, left: 0, opacity: 0, cursor: 'pointer' },
    fileLabel: { color: 'var(--text-main)', fontSize: '1rem' },
    grid: { display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1.5rem' },
    summaryBox: { padding: '2rem', backgroundColor: '#f0fdf4', borderRadius: '12px', margin: '1.5rem 0', color: '#166534', border: '1px solid #bbf7d0' },
    successIcon: { width: '60px', height: '60px', borderRadius: '50%', backgroundColor: '#10b981', color: 'white', fontSize: '2rem', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 1.5rem' },
    actions: { display: 'flex', justifyContent: 'flex-end', gap: '1rem', marginTop: '2rem', paddingTop: '1.5rem', borderTop: '1px solid var(--border)' },
    cancelBtn: { background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer', fontWeight: '600' },
    errorBanner: { backgroundColor: '#fef2f2', color: 'var(--danger)', padding: '1rem', borderRadius: '8px', marginBottom: '1.5rem' },
    select: { width: '100%', padding: '0.75rem', borderRadius: '8px', border: '1px solid var(--border)', backgroundColor: 'white', color: 'var(--text-main)', appearance: 'none' as const },
    summaryGrid: { display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '1rem', margin: '2rem 0' },
    summaryStat: { padding: '1.5rem', backgroundColor: '#f8fafc', borderRadius: '12px', border: '1px solid var(--border)', display: 'flex', flexDirection: 'column' as const, gap: '0.5rem' },
    statLabel: { fontSize: '0.875rem', color: 'var(--text-muted)', textTransform: 'uppercase' as const, letterSpacing: '0.05em' },
    statValue: { fontSize: '1.5rem', fontWeight: '700', color: 'var(--text-main)' },
    performanceBarBase: { height: '8px', backgroundColor: '#e2e8f0', borderRadius: '4px', margin: '2rem 0', overflow: 'hidden' },
    performanceBarFill: { height: '100%', backgroundColor: '#10b981', transition: 'width 1s ease-out' },
    diagnosticHeader: { fontSize: '0.75rem', fontWeight: '800', textTransform: 'uppercase' as const, color: '#991b1b', marginBottom: '0.75rem', letterSpacing: '0.05em' },
    errorLogBox: { textAlign: 'left' as const, marginTop: '2.5rem', padding: '1.5rem', backgroundColor: '#fff1f2', borderRadius: '12px', border: '1px solid #fecaca' },
    errorPre: { whiteSpace: 'pre-wrap' as const, fontSize: '0.85rem', color: '#991b1b', marginTop: '0.5rem', maxHeight: '150px', overflowY: 'auto' as const }
};

export default ImportPage;