import api from './client';
import type { Recommendation, RecommendationRating, SourceScorecard } from './types';

export interface CreateRecommendationData {
  instrumentId: string;
  source: string;
  rating: RecommendationRating;
  date: string;
  targetPrice?: number;
  url?: string;
  comment?: string;
}

export interface UpdateRecommendationData {
  source: string;
  rating: RecommendationRating;
  date: string;
  targetPrice?: number;
  url?: string;
  comment?: string;
}

export const recommendationsApi = {
  getAll: (params?: { instrumentId?: string; source?: string }) =>
    api.get<Recommendation[]>('/recommendations', { params }).then(r => r.data),

  create: (data: CreateRecommendationData) =>
    api.post<Recommendation>('/recommendations', data).then(r => r.data),

  update: (id: string, data: UpdateRecommendationData) =>
    api.put<Recommendation>(`/recommendations/${id}`, data).then(r => r.data),

  remove: (id: string) =>
    api.delete(`/recommendations/${id}`).then(r => r.data),

  getSources: () =>
    api.get<string[]>('/recommendations/sources').then(r => r.data),

  getScorecard: () =>
    api.get<SourceScorecard[]>('/recommendations/scorecard').then(r => r.data),
};
