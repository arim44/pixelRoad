import { Landmark } from './landmark.interface';
export declare class LandmarkService {
    private readonly filePath;
    getAllLandmarks(): Promise<Landmark[]>;
    getLandmarkById(id: number): Promise<Landmark | undefined>;
}
