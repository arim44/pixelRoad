import { Test, TestingModule } from '@nestjs/testing';
import { LandmarkService } from './landmark.service';

describe('LandmarkService', () => {
  let service: LandmarkService;

  beforeEach(async () => {
    const module: TestingModule = await Test.createTestingModule({
      providers: [LandmarkService],
    }).compile();

    service = module.get<LandmarkService>(LandmarkService);
  });

  it('should be defined', () => {
    expect(service).toBeDefined();
  });
});
