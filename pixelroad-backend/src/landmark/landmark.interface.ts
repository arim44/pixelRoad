// 랜드마크 타입
export interface Landmark {
    id: number;
    name: string;
    category: string;
    collectionTitle: string;
    address: string;
    latitude: number;
    longitude: number;
    visitRadius: number;
    thumbnail:string;
    shortDescription: string;
    history: string;
    tags: string[];
    view360Image: string | null;
}