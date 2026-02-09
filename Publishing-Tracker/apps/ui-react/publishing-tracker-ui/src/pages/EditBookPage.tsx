import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { bookService } from '../services/bookService';
import type { UpdateBook } from '../types/book';

const schema = z.object({
    title: z.string().min(1, 'Title is required'),
    author: z.string().min(1, 'Author is required'),
    isbn: z.string().optional().nullable(),
    publicationDate: z.string().optional().nullable(),
    basePrice: z.number().optional().nullable(),
    genre: z.string().optional().nullable(),
    description: z.string().optional().nullable()
});

const EditBookPage = () => {
    const { id } = useParams<{ id: string }>();
    const navigate = useNavigate();
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const { register, handleSubmit, reset, formState: { errors, isDirty } } = useForm<UpdateBook>({
        resolver: zodResolver(schema)
    });

    useEffect(() => {
        const fetchBook = async () => {
            try {
                if (id) {
                    const book = await bookService.getBookById(parseInt(id, 10));
                    // Format date for the input field
                    const formattedDate = book.publicationDate
                        ? new Date(book.publicationDate).toISOString().split('T')[0]
                        : null;

                    reset({
                        ...book,
                        publicationDate: formattedDate
                    });
                }
            } catch {
                setError('Failed to fetch book data.');
            } finally {
                setLoading(false);
            }
        };
        fetchBook();
    }, [id, reset]);

    const onSubmit = async (data: UpdateBook) => {
        if (!id) return;
        try {
            await bookService.updateBook(parseInt(id, 10), data);
            navigate('/books');
        } catch {
            setError('Failed to update book details. Please check your inputs.');
        }
    };

    if (loading) return <div style={pageStyles.centered}>Loading book details...</div>;

    return (
        <div style={pageStyles.wrapper}>
            <div style={pageStyles.header}>
                <h1 style={{ color: '#1e293b' }}>Edit Publication</h1>
                <p style={{ color: 'var(--text-muted)' }}>Update metadata for your work.</p>
            </div>

            <div className="card" style={pageStyles.formCard}>
                {error && <div style={pageStyles.errorBanner}>{error}</div>}

                <form onSubmit={handleSubmit(onSubmit)}>
                    <div className="form-group">
                        <label htmlFor="title">Book Title</label>
                        <input id="title" type="text" {...register('title')} placeholder="e.g. The Great Gatsby" />
                        {errors.title && <span style={pageStyles.errorText}>{errors.title.message}</span>}
                    </div>

                    <div style={pageStyles.row}>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label htmlFor="author">Author</label>
                            <input id="author" type="text" {...register('author')} placeholder="Author name" />
                            {errors.author && <span style={pageStyles.errorText}>{errors.author.message}</span>}
                        </div>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label htmlFor="isbn">ISBN</label>
                            <input id="isbn" type="text" {...register('isbn')} placeholder="978-..." />
                        </div>
                    </div>

                    <div style={pageStyles.row}>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label htmlFor="publicationDate">Publication Date</label>
                            <input id="publicationDate" type="date" {...register('publicationDate')} />
                        </div>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label htmlFor="basePrice">Base Price ($)</label>
                            <input id="basePrice" type="number" step="0.01" {...register('basePrice', { valueAsNumber: true })} inputMode="decimal" />
                        </div>
                    </div>

                    <div className="form-group">
                        <label htmlFor="genre">Genre</label>
                        <input id="genre" type="text" {...register('genre')} placeholder="e.g. Fiction, Science, History" />
                    </div>

                    <div className="form-group">
                        <label htmlFor="description">Description</label>
                        <textarea id="description" rows={5} {...register('description')} placeholder="Brief summary of the book..." />
                    </div>

                    <div style={pageStyles.actions}>
                        <button type="button" onClick={() => navigate('/books')} style={pageStyles.cancelBtn}>
                            Discard Changes
                        </button>
                        <button type="submit" className="btn-primary" disabled={!isDirty}>
                            Save Changes
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};

const pageStyles = {
    wrapper: { maxWidth: '800px', margin: '0 auto' },
    header: { marginBottom: '2rem' },
    formCard: { padding: '2.5rem' },
    row: { display: 'flex', gap: '1.5rem' },
    errorBanner: {
        backgroundColor: '#fef2f2',
        color: 'var(--danger)',
        padding: '1rem',
        borderRadius: '8px',
        marginBottom: '1.5rem',
        border: '1px solid #fee2e2',
        fontSize: '0.9rem',
    },
    errorText: {
        color: 'var(--danger)',
        fontSize: '0.8rem',
        fontWeight: '500',
    },
    actions: {
        display: 'flex',
        justifyContent: 'flex-end',
        gap: '1rem',
        marginTop: '2rem',
        paddingTop: '1.5rem',
        borderTop: '1px solid var(--border)',
    },
    cancelBtn: {
        backgroundColor: 'transparent',
        border: 'none',
        color: 'var(--text-muted)',
        fontWeight: '600',
        cursor: 'pointer',
    },
    centered: { padding: '10rem', textAlign: 'center' as const, color: 'var(--text-muted)', fontSize: '1.2rem' }
};

export default EditBookPage;