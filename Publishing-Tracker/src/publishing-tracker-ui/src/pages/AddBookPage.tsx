import React, { useState } from 'react';
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
            setError('Failed to create book.');
        }
    };

    return (
        <div className="form-container">
            <h1>Add New Book</h1>
            {error && <p style={{ color: 'red' }}>{error}</p>}
            <form onSubmit={handleSubmit(onSubmit)}>
                <div className="input-group">
                    <label htmlFor="title">Title</label>
                    <input id="title" type="text" {...register('title')} />
                    {errors.title && <p style={{ color: 'red' }}>{errors.title.message}</p>}
                </div>
                <div className="input-group">
                    <label htmlFor="author">Author</label>
                    <input id="author" type="text" {...register('author')} />
                    {errors.author && <p style={{ color: 'red' }}>{errors.author.message}</p>}
                </div>
                <div className="input-group">
                    <label htmlFor="isbn">ISBN</label>
                    <input id="isbn" type="text" {...register('isbn')} />
                </div>
                <div className="input-group">
                    <label htmlFor="publicationDate">Publication Date</label>
                    <input id="publicationDate" type="date" {...register('publicationDate', { valueAsDate: true })} />
                </div>
                <div className="input-group">
                    <label htmlFor="basePrice">Base Price</label>
                    <input id="basePrice" type="number" {...register('basePrice', { valueAsNumber: true })} inputMode="decimal" />
                </div>
                <div className="input-group">
                    <label htmlFor="genre">Genre</label>
                    <input id="genre" type="text" {...register('genre')} />
                </div>
                <div className="input-group">
                    <label htmlFor="description">Description</label>
                    <textarea id="description" {...register('description')} />
                </div>
                <button type="submit">Add Book</button>
            </form>
        </div>
    );
};

export default AddBookPage;