import { CreateAiDto } from './dto/create-ai.dto';
import { UpdateAiDto } from './dto/update-ai.dto';
export declare class AiService {
    create(createAiDto: CreateAiDto): string;
    findAll(): string;
    findOne(id: number): string;
    update(id: number, updateAiDto: UpdateAiDto): string;
    remove(id: number): string;
}
