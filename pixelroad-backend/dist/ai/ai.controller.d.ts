import { AiService } from './ai.service';
import { CreateAiDto } from './dto/create-ai.dto';
import { UpdateAiDto } from './dto/update-ai.dto';
export declare class AiController {
    private readonly aiService;
    constructor(aiService: AiService);
    create(createAiDto: CreateAiDto): string;
    findAll(): string;
    findOne(id: string): string;
    update(id: string, updateAiDto: UpdateAiDto): string;
    remove(id: string): string;
}
