import React, { useEffect, useState } from 'react';
import { importService } from '../services/importService';
import { ImportJob } from '../types/import';

const ImportHistoryPage = () => {
    const [history, setHistory] = useState<ImportJob[]>([]);
    const [loading, setLoading] = useState<boolean>(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const fetchHistory = async () => {
            try {
                const historyData = await importService.getHistory();
                setHistory(historyData);
            } catch (err) {
                setError('Failed to fetch import history.');
            } finally {
                setLoading(false);
            }
        };

        fetchHistory();
    }, []);

    if (loading) {
        return <div>Loading...</div>;
    }

    if (error) {
        return <div>{error}</div>;
    }

    return (
        <div>
            <h1>Import History</h1>
            {history.length === 0 ? (
                <p>No import history available.</p>
            ) : (
                <table>
                    <thead>
                        <tr>
                            <th>File Name</th>
                            <th>Status</th>
                            <th>Started At</th>
                            <th>Completed At</th>
                            <th>Records Processed</th>
                            <th>Records Successful</th>
                            <th>Records Failed</th>
                        </tr>
                    </thead>
                    <tbody>
                        {history.map((job) => (
                            <tr key={job.id}>
                                <td>{job.fileName}</td>
                                <td>{job.status}</td>
                                <td>{new Date(job.startedAt).toLocaleString()}</td>
                                <td>{job.completedAt ? new Date(job.completedAt).toLocaleString() : 'N/A'}</td>
                                <td>{job.recordsProcessed}</td>
                                <td>{job.recordsSuccessful}</td>
                                <td>{job.recordsFailed}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}
        </div>
    );
};

export default ImportHistoryPage;