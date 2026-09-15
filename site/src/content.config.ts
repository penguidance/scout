import { defineCollection, z } from 'astro:content';
import { file, glob } from 'astro/loaders';

const localizedText = z.object({ en: z.string().min(1) }).catchall(z.string());

const software = defineCollection({
  loader: file('../data/software-compatibility.json', {
    parser: (text) =>
      JSON.parse(text).map((entry: any) => ({
        ...entry,
        id: entry.match.name_aliases[0].pattern
      }))
  }),
  schema: z.object({
    id: z.string(),
    status: z.enum(['native', 'built_in', 'equivalent', 'partial', 'wine', 'web', 'blocked']),
    notes: localizedText,
    alternatives: z.array(z.object({
      name: z.string(),
      note: localizedText
    })).default([])
  })
});

const pages = defineCollection({
  loader: glob({ pattern: '**/*.md', base: './src/content/pages' })
});

export const collections = { software, pages };