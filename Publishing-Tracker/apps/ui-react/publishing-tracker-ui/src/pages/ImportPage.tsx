import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { importService } from '../services/importService';
import { ImportJob, ColumnMapping } from '../types/import';
import axios from 'axios';

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
        orderId: '',
        defaultCurrency: 'USD'
    });
    const [headers, setHeaders] = useState<string[]>([]);
    const [step, setStep] = useState<'upload' | 'mapping' | 'summary'>('upload');
    const [error, setError] = useState<string | null>(null);
    const [summary, setSummary] = useState<ImportJob | null>(null);
    const [previewData, setPreviewData] = useState<import('../types/import').PreviewData | null>(null);

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
                setPreviewData(data);
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
                console.log("Import result from API:", result);
                console.log("Records successful:", result.recordsSuccessful);
                console.log("Records failed:", result.recordsFailed);
                console.log("Records processed:", result.recordsProcessed);
                setSummary(result);
                setStep('summary');
            } catch (err) {
                let msg = 'Unknown processing error';
                if (axios.isAxiosError(err)) {
                    msg = err.response?.data?.title || err.response?.data?.message || err.message || msg;
                } else if (err instanceof Error) {
                    msg = err.message;
                }

                console.error("Process error:", err);
                setError(`Processing failed: ${msg}`);
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
            <h1 style={{ color: '#1e293b', background: 'none', WebkitTextFillColor: 'initial', opacity: 1, marginBottom: '2rem' }}>Import Sales Data (v2.1)</h1>
            {renderStepper()}

            <div className="card" style={{ padding: '2.5rem' }}>
                {error && <div style={pageStyles.errorBanner}>{error}</div>}

                {step === 'upload' && (
                    <div style={pageStyles.stepContainer}>
                        <h3>Step 1: Upload Data</h3>
                        <p style={{ color: 'var(--text-main)', fontSize: '1.2rem', margin: '1rem 0 2rem 0', opacity: 0.9 }}>
                            Upload sales data from your publishing platform (KDP, IngramSpark, etc.)
                        </p>

                        <div style={{ ...pageStyles.dropZone, padding: '3rem 2rem' }}>
                            <input type="file" onChange={handleFileChange} accept=".csv,.txt" style={pageStyles.fileInput} id="file-upload" />
                            <label htmlFor="file-upload" style={pageStyles.fileLabel}>
                                {file ? (
                                    <>
                                        <div style={{ fontSize: '3rem' }}>📄</div>
                                        <strong>{file.name}</strong>
                                        <span style={{ fontSize: '0.9rem', color: 'var(--text-muted)' }}>Ready to upload</span>
                                    </>
                                ) : (
                                    <>
                                        <div style={{ fontSize: '3rem', marginBottom: '0.5rem', opacity: 1, color: 'var(--primary)' }}>📂</div>
                                        <span className="btn-primary" style={{ display: 'inline-block', padding: '0.8rem 2rem', pointerEvents: 'none', fontSize: '1.1rem', marginBottom: '0.5rem' }}>
                                            Browse Files
                                        </span>
                                        <p style={{ marginTop: '0.5rem', fontSize: '1.2rem', color: 'var(--text-main)', opacity: 0.9, fontWeight: 500 }}>
                                            Upload sales data from your publishing platform
                                        </p>
                                    </>
                                )}
                            </label>
                        </div>

                        <div style={pageStyles.actions}>
                            <button
                                className="btn-primary"
                                onClick={handleUpload}
                                disabled={!file}
                                style={{ opacity: !file ? 0.5 : 1, cursor: !file ? 'not-allowed' : 'pointer' }}
                            >
                                Next: Map Columns
                            </button>
                        </div>
                    </div>
                )}

                {step === 'mapping' && (
                    <div style={pageStyles.stepContainer}>
                        <h3>Step 2: Map Columns</h3>
                        <p style={{ color: 'var(--text-muted)', marginBottom: '1.5rem' }}>
                            Match your file's columns to our fields. Check the preview to ensure data looks correct.
                        </p>

                        <div style={{ marginBottom: '1rem', padding: '1rem', backgroundColor: '#f8fafc', borderRadius: '8px', border: '1px solid var(--border)' }}>
                            <div style={{ marginBottom: '1rem', display: 'flex', alignItems: 'center', gap: '1rem' }}>
                                <label style={{ fontWeight: '600', color: 'var(--text-main)' }}>Default Currency:</label>
                                <select
                                    style={{ ...pageStyles.select, width: 'auto', display: 'inline-block' }}
                                    value={mapping.currency || 'USD'}
                                    onChange={(e) => setMapping(prev => ({ ...prev, currency: e.target.value }))}
                                >
                                    <option value="USD">USD ($)</option>
                                    <option value="GBP">GBP (£)</option>
                                    <option value="EUR">EUR (€)</option>
                                    <option value="CAD">CAD ($)</option>
                                    <option value="AUD">AUD ($)</option>
                                </select>
                                <span style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>Used if no currency column is mapped.</span>
                            </div>

                            <p style={{ margin: 0, fontSize: '0.9rem', color: 'var(--text-muted)' }}>
                                <span style={{ color: 'var(--danger)', fontWeight: 'bold', marginRight: '5px' }}>*</span>
                                <strong style={{ color: 'var(--text-main)' }}>Required Fields:</strong> These columns must be mapped for the import to work.
                            </p>
                            <p style={{ margin: '0.5rem 0 0 0', fontSize: '0.9rem', color: 'var(--text-muted)' }}>
                                <span style={{ display: 'inline-block', width: '10px', height: '10px', marginRight: '5px' }}></span>
                                <strong style={{ color: 'var(--text-main)' }}>Optional Fields:</strong> Can be left blank if your file doesn't have this data.
                            </p>
                        </div>

                        <div style={{ overflowX: 'auto', marginBottom: '2rem' }}>
                            <table style={{ width: '100%', borderCollapse: 'collapse', minWidth: '600px' }}>
                                <thead>
                                    <tr style={{ borderBottom: '2px solid var(--border)' }}>
                                        <th style={pageStyles.th}>Target Field</th>
                                        <th style={pageStyles.th}>Your CSV Column</th>
                                        <th style={pageStyles.th}>Preview (First 3 Rows)</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {(Object.keys(mapping) as Array<keyof ColumnMapping>).filter(k => k !== 'defaultCurrency').map((key) => {
                                        const selectedHeader = mapping[key] as string;
                                        let previewSamples = '';
                                        if (key === 'currency' && !selectedHeader) {
                                            previewSamples = `(Using Default: ${mapping[key] || 'USD'})`;
                                        } else {
                                            previewSamples = previewData?.previewRows.slice(0, 3).map(r => r[selectedHeader]).filter(Boolean).join(', ') || '';
                                        }

                                        return (
                                            <tr key={key} style={{ borderBottom: '1px solid var(--border)' }}>
                                                <td style={{ ...pageStyles.td, fontWeight: '600', width: '25%' }}>
                                                    {key.replace(/([A-Z])/g, ' $1').replace(/^./, str => str.toUpperCase())}
                                                    {['bookTitle', 'platform', 'saleDate', 'quantity', 'unitPrice'].includes(key) ? (
                                                        <span style={{ color: 'var(--danger)', marginLeft: '4px', fontWeight: 'bold' }}>*</span>
                                                    ) : (
                                                        <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)', fontWeight: 'normal', marginLeft: '6px' }}>(Optional)</span>
                                                    )}
                                                </td>
                                                <td style={{ ...pageStyles.td, width: '35%' }}>
                                                    <select
                                                        name={key}
                                                        value={mapping[key]}
                                                        onChange={handleMappingChange}
                                                        style={pageStyles.select}
                                                    >
                                                        <option value="">-- Select Column --</option>
                                                        {headers.map(h => (
                                                            <option key={h} value={h}>{h}</option>
                                                        ))}
                                                    </select>
                                                </td>
                                                <td style={{ ...pageStyles.td, width: '40%', color: 'var(--text-muted)', fontSize: '0.9rem', fontStyle: 'italic' }}>
                                                    {previewSamples || <span style={{ opacity: 0.5 }}>Select a column to preview...</span>}
                                                </td>
                                            </tr>
                                        );
                                    })}
                                </tbody>
                            </table>
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
                        <h3 style={{ marginBottom: '0.5rem' }}>Import Synchronized</h3>
                        <p style={{ fontSize: '1rem', color: '#64748b', marginBottom: '2rem' }}>
                            Processed {summary.recordsProcessed} {summary.recordsProcessed === 1 ? 'record' : 'records'} from <strong>{summary.fileName}</strong>
                        </p>

                        <div style={pageStyles.summaryGrid}>
                            <div style={pageStyles.summaryStat}>
                                <span style={pageStyles.statLabel}>Records Successful</span>
                                <span style={pageStyles.statValue} className="text-success">{summary.recordsSuccessful}</span>
                            </div>
                            <div style={pageStyles.summaryStat}>
                                <span style={pageStyles.statLabel}>Records Failed</span>
                                <span style={pageStyles.statValue} className={summary.recordsFailed > 0 ? "text-danger" : ""}>{summary.recordsFailed}</span>
                            </div>
                            <div style={pageStyles.summaryStat}>
                                <span style={pageStyles.statLabel}>Success Rate</span>
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
        padding: '4rem 2rem',
        textAlign: 'center' as const,
        backgroundColor: 'rgba(255, 255, 255, 0.03)', // Dark glass effect
        cursor: 'pointer',
        position: 'relative' as const,
        transition: 'all 0.3s ease'
    },
    fileInput: { position: 'absolute' as const, width: '100%', height: '100%', top: 0, left: 0, opacity: 0, cursor: 'pointer', zIndex: 10 },
    fileLabel: { color: 'var(--text-main)', fontSize: '1.1rem', display: 'flex', flexDirection: 'column' as const, alignItems: 'center', gap: '1rem' },
    grid: { display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1.5rem' },
    summaryBox: { padding: '2rem', backgroundColor: 'rgba(16, 185, 129, 0.1)', borderRadius: '12px', margin: '1.5rem 0', color: '#34d399', border: '1px solid rgba(16, 185, 129, 0.2)' },
    successIcon: { width: '60px', height: '60px', borderRadius: '50%', backgroundColor: '#10b981', color: 'white', fontSize: '2rem', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 1.5rem' },
    actions: { display: 'flex', justifyContent: 'flex-end', gap: '1rem', marginTop: '2rem', paddingTop: '1.5rem', borderTop: '1px solid var(--border)' },
    cancelBtn: { background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer', fontWeight: '600' },
    errorBanner: { backgroundColor: '#fef2f2', color: 'var(--danger)', padding: '1rem', borderRadius: '8px', marginBottom: '1.5rem' },
    select: { width: '100%', padding: '0.75rem', borderRadius: '8px', border: '1px solid var(--border)', backgroundColor: '#1e293b', color: 'var(--text-main)', appearance: 'none' as const },
    summaryGrid: { display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '1rem', margin: '2rem 0' },
    summaryStat: { padding: '1.5rem', backgroundColor: '#f8fafc', borderRadius: '12px', border: '1px solid var(--border)', display: 'flex', flexDirection: 'column' as const, gap: '0.5rem' },
    statLabel: { fontSize: '0.875rem', color: 'var(--text-muted)', textTransform: 'uppercase' as const, letterSpacing: '0.05em' },
    statValue: { fontSize: '1.5rem', fontWeight: '700', color: '#1e293b' },
    performanceBarBase: { height: '8px', backgroundColor: '#e2e8f0', borderRadius: '4px', margin: '2rem 0', overflow: 'hidden' },
    performanceBarFill: { height: '100%', backgroundColor: '#10b981', transition: 'width 1s ease-out' },
    diagnosticHeader: { fontSize: '0.75rem', fontWeight: '800', textTransform: 'uppercase' as const, color: '#991b1b', marginBottom: '0.75rem', letterSpacing: '0.05em' },
    errorLogBox: { textAlign: 'left' as const, marginTop: '2.5rem', padding: '1.5rem', backgroundColor: '#fff1f2', borderRadius: '12px', border: '1px solid #fecaca' },
    errorPre: { whiteSpace: 'pre-wrap' as const, fontSize: '0.85rem', color: '#991b1b', marginTop: '0.5rem', maxHeight: '150px', overflowY: 'auto' as const },
    th: { textAlign: 'left' as const, padding: '1rem', color: 'var(--text-muted)', borderBottom: '2px solid var(--border)', fontSize: '0.9rem', textTransform: 'uppercase' as const, letterSpacing: '0.05em' },
    td: { padding: '1rem', borderBottom: '1px solid var(--border)', color: 'var(--text-main)' }
};

export default ImportPage;