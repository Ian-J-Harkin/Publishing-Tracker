import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { bookService } from '../services/bookService';
import type { CreateBook } from '../types/book';

const schema = z.object({
    title: z.string().min(1, 'Title is required'),
    author: z.string().min(1, 'Author is required'),
    isbn: z.string().optional(),
    publicationDate: z.date().optional(),
    basePrice: z.number().optional(),
    genre: z.string().optional(),
    description: z.string().optional()
});

const AddBookPage = () => {
    const { register, handleSubmit, formState: { errors } } = useForm<CreateBook>({
        resolver: zodResolver(schema)
    });
    const [error, setError] = useState<string | null>(null);
    const navigate = useNavigate();

    const onSubmit = async (data: CreateBook) => {
        try {
            await bookService.createBook(data);
            navigate('/books');
        } catch (err) {
            setError('Failed to create book. Please check your inputs.');
        }
    };

    return (
        <div style={pageStyles.wrapper}>
            <div style={pageStyles.header}>
                <h1>Add New Book</h1>
                <p style={{ color: 'var(--text-muted)' }}>Fill in the details to add a new title to your library.</p>
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
                            <label htmlFor="isbn">ISBN (Optional)</label>
                            <input id="isbn" type="text" {...register('isbn')} placeholder="978-..." />
                        </div>
                    </div>

                    <div style={pageStyles.row}>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label htmlFor="publicationDate">Publication Date</label>
                            <input id="publicationDate" type="date" {...register('publicationDate', { valueAsDate: true })} />
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
                        <textarea id="description" rows={4} {...register('description')} placeholder="Brief summary of the book..." />
                    </div>

                    <div style={pageStyles.actions}>
                        <button type="button" onClick={() => navigate('/books')} style={pageStyles.cancelBtn}>
                            Cancel
                        </button>
                        <button type="submit" className="btn-primary">
                            Create Book
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};

const pageStyles = {
    wrapper: {
        maxWidth: '800px', // Prevents the form from being too wide
        margin: '0 auto',
    },
    header: {
        marginBottom: '2rem',
    },
    formCard: {
        padding: '2.5rem',
    },
    row: {
        display: 'flex',
        gap: '1.5rem',
    },
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
    }
};

export default AddBookPage;